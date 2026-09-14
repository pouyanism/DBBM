using BlockingMonitor.Abstractions;

namespace BlockingMonitor.Services;

/// <summary>
/// هر 2 ثانیه یک‌بار سرور فعال رو Poll می‌کنه، نتیجه رو به Chainهای درختی تبدیل می‌کنه،
/// و از طریق IBlockingSnapshotPublisher منتشرش می‌کنه.
///
/// این کلاس عمداً به هیچ پیاده‌سازی خاصی (HTTP polling از فرانت یا SignalR) وابسته نیست -
/// فقط IBlockingSnapshotPublisher.Publish() رو صدا می‌زنه.
/// </summary>
public sealed class PollerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IBlockingDataReader _dataReader;
    private readonly IActiveServerProvider _serverProvider;
    private readonly ChainTracker _chainTracker;
    private readonly IBlockingSnapshotPublisher _publisher;
    private readonly ILogger<PollerService> _logger;

    public PollerService(
        IBlockingDataReader dataReader,
        IActiveServerProvider serverProvider,
        ChainTracker chainTracker,
        IBlockingSnapshotPublisher publisher,
        ILogger<PollerService> logger)
    {
        _dataReader = dataReader;
        _serverProvider = serverProvider;
        _chainTracker = chainTracker;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // تا زمانی که کاربر Connect نکرده، هیچ Query اجرا نشود
            if (!_serverProvider.HasActiveConnection())
            {
                _logger.LogDebug("No active database connection. Waiting for user to connect...");
                continue;
            }

            try
            {
                var rawRows = await _dataReader.GetBlockingSessionsAsync(stoppingToken);

                var snapshot = _chainTracker.ProcessNewData(
                    rawRows,
                    _serverProvider.ServerName);

                _publisher.Publish(snapshot);

                if (snapshot.HasActiveBlocking)
                {
                    _logger.LogInformation(
                        "{ChainCount} زنجیره‌ی بلاکینگ فعال شناسایی شد.",
                        snapshot.ActiveChains.Count);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "اجرای یک دور Poll با خطا مواجه شد؛ در دور بعدی دوباره تلاش می‌شود.");
            }
        }
    }
}
