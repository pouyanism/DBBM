using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using BlockingMonitor.Abstractions;

namespace BlockingMonitor.Controller;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly IActiveServerProvider _serverProvider;
    private readonly ILogger<SessionController> _logger;

    public SessionController(IActiveServerProvider serverProvider, ILogger<SessionController> logger)
    {
        _serverProvider = serverProvider;
        _logger = logger;
    }

    private const string PermissionCheckSql = @"
        SELECT CASE WHEN IS_SRVROLEMEMBER('sysadmin') = 1
                     OR IS_SRVROLEMEMBER('processadmin') = 1
                     OR HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY CONNECTION') = 1
                THEN 1 ELSE 0 END";

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        if (!_serverProvider.HasActiveConnection())
            return BadRequest(new { canKill = false, message = "هیچ اتصالی فعال نیست." });

        try
        {
            await using var conn = new SqlConnection(_serverProvider.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(PermissionCheckSql, conn);
            var result = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return Ok(new { canKill = result == 1 });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "بررسی دسترسی Kill ناموفق بود.");
            return Ok(new { canKill = false, message = "بررسی دسترسی ناموفق بود." });
        }
    }

    [HttpPost("kill/{sessionId:int}")]
    public async Task<IActionResult> KillSession(int sessionId)
    {
        if (!_serverProvider.HasActiveConnection())
            return BadRequest(new { success = false, message = "هیچ اتصالی فعال نیست." });

        try
        {
            await using var conn = new SqlConnection(_serverProvider.GetConnectionString());
            await conn.OpenAsync();

            // چک مجدد دسترسی سمت سرور - هیچ‌وقت فقط به چک قبلیِ کلاینت اعتماد نکن
            await using (var permCmd = new SqlCommand(PermissionCheckSql, conn))
            {
                var canKill = (int)(await permCmd.ExecuteScalarAsync() ?? 0) == 1;
                if (!canKill)
                    return StatusCode(403, new { success = false, message = "شما دسترسی Kill کردن Session را ندارید." });
            }

            // sessionId از route با {sessionId:int} Bind شده (فقط int واقعی قبول میشه)،
            // پس خطر SQL Injection در این String Interpolation وجود نداره.
            await using var killCmd = new SqlCommand($"KILL {sessionId};", conn);
            await killCmd.ExecuteNonQueryAsync();

            return Ok(new { success = true, message = $"Session {sessionId} با موفقیت Kill شد." });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Kill کردن Session {SessionId} ناموفق بود.", sessionId);
            return Ok(new { success = false, message = $"Kill ناموفق بود: {ex.Message}" });
        }
    }
}