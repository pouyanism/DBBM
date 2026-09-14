using BlockingMonitor.Models;

namespace BlockingMonitor.Services;

/// <summary>
/// صرفاً یک Adjacency List (لیست Flat با parent_session_id) رو به یک Tree تبدیل می‌کنه.
/// هیچ تصمیمی درباره‌ی "کی Head Blocker هست" یا "عمق چقدره" اینجا گرفته نمیشه -
/// اون‌ها از قبل در Sql/BlockingQuery.sql محاسبه و در خود داده موجودن.
/// این کلاس فقط Group کردن و Nest کردنه؛ اگه فردا منطق تشخیص زنجیره در SQL عوض بشه،
/// این کلاس بدون تغییر باقی می‌مونه چون فقط به ستون‌های head_blocker_session_id و
/// parent_session_id متکیه، نه به نحوه‌ی محاسبه‌شون.
/// </summary>
public static class ChainBuilder
{
    public static List<BlockingChain> BuildChains(IReadOnlyCollection<SessionRow> rows, DateTime nowUtc)
    {
        if (rows.Count == 0) return [];

        // گروه‌بندی بر اساس ریشه‌ی زنجیره (head_blocker_session_id) - این کلید طبیعی هر Chain هست
        var byHeadBlocker = rows.GroupBy(r => r.HeadBlockerSessionId);

        var chains = new List<BlockingChain>();

        foreach (var group in byHeadBlocker)
        {
            var rowsInChain = group.ToList();
            var headRow = rowsInChain.FirstOrDefault(r => r.IsHeadBlocker);

            // اگه به هر دلیلی (مثلاً race condition بین خواندن و تغییر وضعیت سرور) ردیف
            // خودِ Head Blocker موجود نبود، این Chain رو نادیده می‌گیریم؛ داده‌ی ناقص بهتره
            // نمایش داده نشه تا اینکه یک Tree نصفه و گمراه‌کننده نشون بدیم.
            if (headRow is null) continue;

            var rootNode = BuildNodeRecursive(headRow.SessionId, rowsInChain);

            chains.Add(new BlockingChain
            {
                HeadBlockerSessionId = group.Key,
                Root = rootNode,
                FirstDetectedAtUtc = nowUtc,   // ChainTracker این مقدار رو در Merge با State قبلی اصلاح می‌کنه
                LastSeenAtUtc = nowUtc
            });
        }

        return chains;
    }

    private static BlockingChainNode BuildNodeRecursive(int sessionId, List<SessionRow> allRowsInChain)
    {
        var self = allRowsInChain.First(r => r.SessionId == sessionId);

        // فرزندان مستقیم: کسانی که parent_session_id شون دقیقاً همین session هست
        // (به‌جز خودش، برای جلوگیری از حلقه‌ی بی‌نهایت در سناریوی self-blocking)
        var directChildren = allRowsInChain
            .Where(r => r.ParentSessionId == sessionId && r.SessionId != sessionId)
            .Select(r => BuildNodeRecursive(r.SessionId, allRowsInChain))
            .ToList();

        return new BlockingChainNode
        {
            Session = self,
            Children = directChildren
        };
    }
}
