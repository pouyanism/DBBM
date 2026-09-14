using BlockingMonitor.Abstractions;
using BlockingMonitor.Hubs;
using BlockingMonitor.Models;
using Microsoft.AspNetCore.SignalR;

namespace BlockingMonitor.Services;

/// <summary>
/// این کلاس هم InMemorySnapshotPublisher رو (برای REST endpoint و برای
/// OnConnectedAsync کلاینت‌های تازه‌وصل‌شده) آپدیت می‌کنه، هم به‌صورت
/// Fire-and-Forget به همه‌ی کلاینت‌های Hub Push می‌کنه.
/// PollerService/ChainTracker از این تغییر کاملاً بی‌خبرن - فقط
/// IBlockingSnapshotPublisher.Publish() رو صدا می‌زنن.
/// </summary>
public sealed class SignalRSnapshotPublisher : IBlockingSnapshotPublisher
{
    private readonly InMemorySnapshotPublisher _cache;
    private readonly IHubContext<BlockingHub> _hub;
    private readonly ILogger<SignalRSnapshotPublisher> _logger;

    public SignalRSnapshotPublisher(
        InMemorySnapshotPublisher cache,
        IHubContext<BlockingHub> hub,
        ILogger<SignalRSnapshotPublisher> logger)
    {
        _cache = cache;
        _hub = hub;
        _logger = logger;
    }

    public void Publish(BlockingSnapshot snapshot)
    {
        _cache.Publish(snapshot);
        _ = PushToHubSafeAsync(snapshot);
    }

    private async Task PushToHubSafeAsync(BlockingSnapshot snapshot)
    {
        try
        {
            await _hub.Clients.All.SendAsync("SnapshotUpdated", snapshot);
        }
        catch (Exception ex)
        {
            // Push یک incident نباید کل Poll Loop رو Crash بده؛ فقط لاگ کن.
            _logger.LogWarning(ex, "ارسال Snapshot به کلاینت‌های SignalR ناموفق بود.");
        }
    }
}