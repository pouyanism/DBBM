namespace BlockingMonitor.Models;

/// <summary>
/// نگاشت مستقیم و بدون تغییر یک ردیف از خروجی Sql/BlockingQuery.sql.
/// این کلاس هیچ منطقی نداره؛ فقط Data Transfer Object خامه.
/// هر تصمیم منطقی (Head Blocker کیه، عمق چقدره) قبلاً در SQL محاسبه شده.
/// </summary>
public sealed class SessionRow
{
    public int SessionId { get; init; }
    public string? Status { get; init; }
    public string? Command { get; init; }
    public string? WaitType { get; init; }
    public long? WaitDurationMs { get; init; }
    public string? ResourceDescription { get; init; }
    public string? ResourceType { get; init; }
    public string? LockedObjectName { get; init; }

    public int? ParentSessionId { get; init; }
    public int HeadBlockerSessionId { get; init; }
    public int Depth { get; init; }
    public bool IsHeadBlocker { get; init; }
    public bool IsSelfBlocking { get; init; }

    public string? DatabaseName { get; init; }
    public string? LoginName { get; init; }
    public string? HostName { get; init; }
    public string? ProgramName { get; init; }
    public int OpenTransactionCount { get; init; }
    public DateTime? TransactionBeginTime { get; init; }
    public long? TransactionAgeSeconds { get; init; }
    public string? TransactionType { get; init; }
    public DateTime? LastRequestStartTime { get; init; }
    public DateTime? LastRequestEndTime { get; init; }
    public long? IdleSeconds { get; init; }
    public long? TotalElapsedTimeMs { get; init; }
    public long? CpuTimeMs { get; init; }
    public long? Reads { get; init; }
    public long? Writes { get; init; }
    public float? PercentComplete { get; init; }
    public string? SqlText { get; init; }
}
