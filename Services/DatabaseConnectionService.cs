using BlockingMonitor.Models;
using Microsoft.Data.SqlClient;

namespace BlockingMonitor.Services
{
    public class DatabaseConnectionService
    {
        private readonly ConnectionStringBuilder _connectionBuilder;
        private readonly ILogger<DatabaseConnectionService> _logger;
        private string _currentConnectionString;
        private bool _isConnected;
        public bool HasActiveConnection => _isConnected;
        public string? CurrentServerName { get; private set; }
        public string? CurrentDatabaseName { get; private set; }

        public DatabaseConnectionService(ConnectionStringBuilder connectionBuilder,
                                         ILogger<DatabaseConnectionService> logger)
        {
            _connectionBuilder = connectionBuilder;
            _logger = logger;
        }

        public async Task<bool> ConnectAsync(ConnectionRequest request)
        {
            try
            {
                var credentials = new ServerCredentials
                {
                    ServerName = request.ServerName,
                    DatabaseName = request.DatabaseName,
                    Username = request.Username,
                    Password = request.Password,
                    UseWindowsAuth = request.UseWindowsAuth,
                    Timeout = request.Timeout
                };

                _currentConnectionString = _connectionBuilder.BuildConnectionString(credentials);

                // تست اتصال
                using var connection = new SqlConnection(_currentConnectionString);
                await connection.OpenAsync();

                _isConnected = true;
                CurrentServerName = request.ServerName;
                CurrentDatabaseName = request.DatabaseName;
                _logger.LogInformation($"Connected to database: {request.DatabaseName} on {request.ServerName}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to database");
                _isConnected = false;
                return false;
            }
        }

        public string GetConnectionString()
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected to database");

            return _currentConnectionString;
        }

        public void Disconnect()
        {
            _isConnected = false;
            _currentConnectionString = null;
            CurrentServerName = null;
            CurrentDatabaseName = null;
        }
    }
}
