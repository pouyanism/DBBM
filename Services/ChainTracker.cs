using System.Collections.Concurrent;
using BlockingMonitor.Models;

namespace BlockingMonitor.Services;

/// <summary>
/// بین هر دو Poll، State رو نگه می‌داره تا:
/// 1) هر Chain به‌جای اینکه هر بار "جدید" به‌حساب بیاد، به‌روزرسانی بشه (Deduplication).
/// 2) عمر واقعی هر Chain (FirstDetectedAtUtc) حفظ بشه، نه اینکه هر Poll از صفر شروع بشه.
/// 3) Chainهایی که دیگه در Poll جدید دیده نمیشن (یعنی حل شدن) از لیست فعال حذف بشن.
///
/// این کلاس Singleton هست و باید Thread-safe باشه چون در آینده ممکنه چند مصرف‌کننده
/// (مثلاً هم HTTP endpoint هم یک SignalR broadcaster) هم‌زمان بخوانن.
/// </summary>
public sealed class ChainTracker
{
    // کلید = HeadBlockerSessionId؛ این شناسه‌ی پایدار هر Chain هست، تا وقتی ریشه‌ی
    // زنجیره عوض نشه (که یعنی واقعاً یک Incident متفاوته)، همون Chain به‌روزرسانی میشه.
    private readonly ConcurrentDictionary<int, BlockingChain> _activeChains = new();

    public BlockingSnapshot ProcessNewData(IReadOnlyCollection<SessionRow> rawRows, string serverName)
    {
        var now = DateTime.UtcNow;
        var freshChains = ChainBuilder.BuildChains(rawRows, now);
        var freshHeadBlockerIds = freshChains.Select(c => c.HeadBlockerSessionId).ToHashSet();

        foreach (var fresh in freshChains)
        {
            _activeChains.AddOrUpdate(
                fresh.HeadBlockerSessionId,
                addValueFactory: _ => fresh,
                updateValueFactory: (_, existing) =>
                {
                    // عمر واقعی Chain رو از نسخه‌ی قبلی حفظ کن؛ فقط LastSeen و محتوا آپدیت میشه
                    fresh.LastSeenAtUtc = now;
                    return new BlockingChain
                    {
                        HeadBlockerSessionId = fresh.HeadBlockerSessionId,
                        Root = fresh.Root,
                        FirstDetectedAtUtc = existing.FirstDetectedAtUtc,
                        LastSeenAtUtc = now
                    };
                });
        }

        // هر Chain ای که در این دور دیده نشد یعنی دیگه بلاکینگ فعالی نداره -> حذفش کن
        var resolvedKeys = _activeChains.Keys.Where(k => !freshHeadBlockerIds.Contains(k)).ToList();
        foreach (var key in resolvedKeys)
        {
            _activeChains.TryRemove(key, out _);
        }

        return new BlockingSnapshot
        {
            CapturedAtUtc = now,
            ServerName = serverName,
            ActiveChains = _activeChains.Values
                .OrderByDescending(c => c.OldestTransactionAgeSeconds)
                .ToList()
        };
    }
}
