using BlockingMonitor.Abstractions;

namespace BlockingMonitor.Services;

public sealed class ActiveServerProvider : IActiveServerProvider
{
    private readonly IConfiguration _configuration;
    private readonly DatabaseConnectionService _dbService;

    public ActiveServerProvider(IConfiguration configuration, DatabaseConnectionService dbService)
    {
        _configuration = configuration;
        _dbService = dbService;
    }

    public string ServerName =>
        _dbService.HasActiveConnection
        ? $"{_dbService.CurrentServerName}/{_dbService.CurrentDatabaseName}"
        : "Not Connected";

    public string GetConnectionString() => _dbService.GetConnectionString();

    public bool HasActiveConnection() => _dbService.HasActiveConnection;

   

    private string GetConnectionStringFromConfig()
    {
        var connString = _configuration.GetConnectionString("MonitoredServer");

        if (string.IsNullOrEmpty(connString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MonitoredServer در appsettings تنظیم نشده و هیچ اتصال فعالی وجود ندارد.");
        }

        return connString;
    }

}

