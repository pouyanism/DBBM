using Microsoft.AspNetCore.SignalR;

namespace BlockingMonitor.Hubs;

/// <summary>
/// نقطه‌ی اتصال کلاینت‌ها (فلاتر). خودش هیچ منطقی نداره جز اینکه به
/// کلاینت تازه‌وصل‌شده فوراً آخرین Snapshot موجود رو بده - تا تا ۲ ثانیه‌ی
/// بعدی (اولین Push جدید) صفحه خالی نمونه.
/// </summary>
public sealed class BlockingHub : Hub
{
    private readonly Services.InMemorySnapshotPublisher _cache;

    public BlockingHub(Services.InMemorySnapshotPublisher cache) => _cache = cache;

    public override async Task OnConnectedAsync()
    {
        var latest = _cache.GetLatest();
        if (latest is not null)
        {
            await Clients.Caller.SendAsync("SnapshotUpdated", latest);
        }
        await base.OnConnectedAsync();
    }
}