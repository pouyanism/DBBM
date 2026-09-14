using BlockingMonitor.Abstractions;
using BlockingMonitor.Models;
using Microsoft.Data.SqlClient;

namespace BlockingMonitor.Services;

/// <summary>
/// اجرای Sql/BlockingQuery.sql روی سرور فعال و نگاشت نتیجه به SessionRow.
/// این کلاس عمداً هیچ منطق تصمیم‌گیری نداره - همه‌چیز از قبل در خود SQL محاسبه شده.
/// </summary>
public sealed class SqlBlockingDataReader : IBlockingDataReader
{
    private readonly IActiveServerProvider _serverProvider;
    private readonly ILogger<SqlBlockingDataReader> _logger;
    private readonly string _queryText;

    // Command Timeout کوتاه و عمدی: این کوئری باید همیشه زیر یک ثانیه اجرا بشه.
    // اگه به این Timeout خورد، یعنی یک مشکل واقعی (مثلاً خود سرور SQL شدیداً تحت فشاره)
    // و بهتره Poll بعدی دوباره تلاش کنه تا اینکه کل حلقه رو برای مدت طولانی معطل نگه داریم.
    private const int CommandTimeoutSeconds = 5;

    public SqlBlockingDataReader(IActiveServerProvider serverProvider, ILogger<SqlBlockingDataReader> logger)
    {
        _serverProvider = serverProvider;
        _logger = logger;

        // فایل SQL کنار اجرایی کپی میشه (به‌خاطر تنظیم CopyToOutputDirectory در csproj)
        // و اینجا یک‌بار در Startup خونده میشه تا هر Poll دوباره از دیسک نخونه.
        var sqlFilePath = Path.Combine(AppContext.BaseDirectory, "Sql", "BlockingQuery.sql");
        _queryText = File.ReadAllText(sqlFilePath);
    }

    public async Task<List<SessionRow>> GetBlockingSessionsAsync(CancellationToken cancellationToken)
    {
        var result = new List<SessionRow>();

        await using var connection = new SqlConnection(_serverProvider.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(_queryText, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapRow(reader));
        }

        return result;
    }

    private static SessionRow MapRow(SqlDataReader r) => new()
    {
        SessionId = r.GetInt16(r.GetOrdinal("session_id")),
        Status = GetStringOrNull(r, "status"),
        Command = GetStringOrNull(r, "command"),
        WaitType = GetStringOrNull(r, "wait_type"),
        WaitDurationMs = GetInt64OrNull(r, "wait_duration_ms"),
        ResourceDescription = GetStringOrNull(r, "resource_description"),
        ResourceType = GetStringOrNull(r, "resource_type"),
        //LockedObjectName = GetStringOrNull(r, "locked_object_name"),

        ParentSessionId = GetInt16OrNull(r, "parent_session_id"),
        HeadBlockerSessionId = r.GetInt16(r.GetOrdinal("head_blocker_session_id")),
        Depth = r.GetInt32(r.GetOrdinal("depth")),
        IsHeadBlocker = r.GetInt32(r.GetOrdinal("is_head_blocker")) == 1,
        IsSelfBlocking = r.GetInt32(r.GetOrdinal("is_self_blocking")) == 1,

        DatabaseName = GetStringOrNull(r, "database_name"),
        LoginName = GetStringOrNull(r, "login_name"),
        HostName = GetStringOrNull(r, "host_name"),
        ProgramName = GetStringOrNull(r, "program_name"),
        OpenTransactionCount = r.GetInt32(r.GetOrdinal("open_transaction_count")),
        TransactionBeginTime = GetDateTimeOrNull(r, "transaction_begin_time"),
        TransactionAgeSeconds = GetInt64OrNull(r, "transaction_age_seconds"),
        TransactionType = GetStringOrNull(r, "transaction_type"),
        LastRequestStartTime = GetDateTimeOrNull(r, "last_request_start_time"),
        LastRequestEndTime = GetDateTimeOrNull(r, "last_request_end_time"),
        IdleSeconds = GetInt64OrNull(r, "idle_seconds"),
        TotalElapsedTimeMs = GetInt64OrNull(r, "total_elapsed_time"),
        CpuTimeMs = GetInt64OrNull(r, "cpu_time"),
        Reads = GetInt64OrNull(r, "reads"),
        Writes = GetInt64OrNull(r, "writes"),
        PercentComplete = GetFloatOrNull(r, "percent_complete"),
        SqlText = GetStringOrNull(r, "sql_text"),
    };

    // --- Helperهای Null-Safe؛ چون اکثر ستون‌های این کوئری (به‌خاطر LEFT JOINهای متعدد) می‌تونن NULL باشن ---

    private static string? GetStringOrNull(SqlDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        return r.IsDBNull(ord) ? null : r.GetString(ord);
    }

    private static float? GetFloatOrNull(SqlDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        return r.IsDBNull(ord) ? null : r.GetFloat(ord);
    }

    private static short? GetInt16OrNull(SqlDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        return r.IsDBNull(ord) ? null : r.GetInt16(ord);
    }

    private static long? GetInt64OrNull(SqlDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        if (r.IsDBNull(ord)) return null;
        // total_elapsed_time/cpu_time/reads/writes در sys.dm_exec_requests از نوع int/bigint هستن؛
        // برای اطمینان از هر دو حالت پشتیبانی می‌کنیم.
        return Convert.ToInt64(r.GetValue(ord));
    }

    private static DateTime? GetDateTimeOrNull(SqlDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        return r.IsDBNull(ord) ? null : r.GetDateTime(ord);
    }
}
