using BlockingMonitor.Abstractions;
using BlockingMonitor.Models;
using BlockingMonitor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BlockingMonitor.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionController : ControllerBase
    {
        private readonly DatabaseConnectionService _dbService;
        private readonly IActiveServerProvider _serverProvider;
        private readonly ILogger<ConnectionController> _logger;

        public ConnectionController(
        DatabaseConnectionService dbService,
        IActiveServerProvider serverProvider,
        ILogger<ConnectionController> logger)
        {
            _dbService = dbService;
            _serverProvider = serverProvider;
            _logger = logger;
        }

        // دریافت وضعیت فعلی اتصال
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var isConnected = _serverProvider.HasActiveConnection();
            return Ok(new
            {
                IsConnected = isConnected,
                ServerName = _serverProvider.ServerName,
                HasActiveConnection = isConnected,
                Message = isConnected ? "Connected to database" : "Not connected"
            });
        }

        // اتصال به دیتابیس با اطلاعات وارد شده
        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] ConnectionViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var request = new ConnectionRequest
                {
                    ServerName = model.ServerName,
                    DatabaseName = model.DatabaseName,
                    Username = model.Username,
                    Password = model.Password,
                    UseWindowsAuth = model.UseWindowsAuth,
                    Timeout = model.Timeout
                };

                var success = await _dbService.ConnectAsync(request);

                if (success)
                {
                    _logger.LogInformation($"User connected to {model.ServerName}/{model.DatabaseName}");
                    return Ok(new
                    {
                        Success = true,
                        Message = "Connected successfully",
                        ServerName = _serverProvider.ServerName
                    });
                }

                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Connection failed. Please check your credentials."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to database");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Connection error: {ex.Message}"
                });
            }
        }

        // قطع اتصال
        [HttpPost("disconnect")]
        public IActionResult Disconnect()
        {
            _dbService.Disconnect();
            //_publisher.Clear(); // نیاز به Inject کردن InMemorySnapshotPublisher در Constructor
            return Ok(new
            {
                Success = true,
                Message = "Disconnected successfully"
            });
        }

        // تست اتصال بدون ذخیره کردن
        [HttpPost("test")]
        public async Task<IActionResult> TestConnection([FromBody] ConnectionViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var request = new ConnectionRequest
                {
                    ServerName = model.ServerName,
                    DatabaseName = "master",
                    Username = model.Username,
                    Password = model.Password,
                    UseWindowsAuth = model.UseWindowsAuth,
                    Timeout = model.Timeout
                };

                // تست اتصال با یک نمونه موقت
                var builder = new ConnectionStringBuilder(null, null);
                var testConnString = builder.BuildConnectionString(new ServerCredentials
                {
                    ServerName = request.ServerName,
                    DatabaseName = "master",
                    Username = request.Username,
                    Password = request.Password,
                    UseWindowsAuth = request.UseWindowsAuth,
                    Timeout = request.Timeout
                });

                using var connection = new SqlConnection(testConnString);
                await connection.OpenAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "Connection test successful"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Success = false,
                    Message = $"Connection test failed: {ex.Message}"
                });
            }
        }
    }
}
