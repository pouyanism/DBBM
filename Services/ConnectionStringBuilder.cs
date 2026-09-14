namespace BlockingMonitor.Services
{
    public class ConnectionStringBuilder
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ConnectionStringBuilder(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public string BuildConnectionString(ServerCredentials credentials)
        {
            if (credentials.UseWindowsAuth)
            {
                return $"Server={credentials.ServerName};" +
                   $"Database={credentials.DatabaseName};" +
                   $"Trusted_Connection=True;" +
                   $"TrustServerCertificate=True;" +
                   $"Connection Timeout={credentials.Timeout ?? 30};" +
                   $"MultipleActiveResultSets=True;";
            }
            else
            {
                return $"Server={credentials.ServerName};" +
                   $"Database={credentials.DatabaseName};" +
                   $"User Id={credentials.Username};" +
                   $"Password={credentials.Password};" +
                   $"TrustServerCertificate=True;" +
                   $"Connection Timeout={credentials.Timeout ?? 30};" +
                   $"MultipleActiveResultSets=True;";
            }
        }

        private string BuildWindowsAuthConnection(ServerCredentials credentials)
        {
            return $"Server={credentials.ServerName};" +
                   $"Database={credentials.DatabaseName};" +
                   $"Trusted_Connection=True;" +
                   $"TrustServerCertificate=True;" +
                   $"Connection Timeout={credentials.Timeout ?? 30};";
        }

        private string BuildSqlAuthConnection(ServerCredentials credentials)
        {
            return $"Server={credentials.ServerName};" +
                   $"Database={credentials.DatabaseName};" +
                   $"User Id={credentials.Username};" +
                   $"Password={credentials.Password};" +
                   $"TrustServerCertificate=True;" +
                   $"Connection Timeout={credentials.Timeout ?? 30};";
        }
    }

    public class ServerCredentials
    {
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseWindowsAuth { get; set; }
        public int? Timeout { get; set; }
    }
}
