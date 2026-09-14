namespace BlockingMonitor.Models;

/// <summary>
/// یک گره در درخت زنجیره‌ی بلاکینگ. هر گره یک SessionRow به همراه فرزندانش (کسانی که
/// این session مستقیماً بلاکشون کرده) رو نگه می‌داره.
/// </summary>
public sealed class BlockingChainNode
{
    public required SessionRow Session { get; init; }
    public List<BlockingChainNode> Children { get; init; } = [];

    /// <summary>مجموع همه‌ی نوادگان (فرزند، نوه، ...) زیر این گره.</summary>
    public int DescendantCount => Children.Sum(c => 1 + c.DescendantCount);
}

/// <summary>
/// یک زنجیره‌ی کامل بلاکینگ، از ریشه (Head Blocker) به پایین.
/// این همون واحدیه که در UI به‌صورت یک Card بزرگ نمایش داده میشه.
/// </summary>
public sealed class BlockingChain
{
    public required int HeadBlockerSessionId { get; init; }
    public required BlockingChainNode Root { get; init; }

    /// <summary>اولین باری که این زنجیره (با همین Head Blocker) دیده شده - برای محاسبه‌ی عمر واقعی incident.</summary>
    public DateTime FirstDetectedAtUtc { get; init; }

    /// <summary>آخرین باری که این زنجیره در یک Poll دیده شده - برای تشخیص Stale/Resolved.</summary>
    public DateTime LastSeenAtUtc { get; set; }

    public int TotalBlockedSessions => Root.DescendantCount;

    public int MaxDepth => CalculateMaxDepth(Root);

    /// <summary>بیشترین transaction_age_seconds در کل زنجیره - معیار "چقدر این incident جدیه".</summary>
    public long OldestTransactionAgeSeconds => CalculateMaxAge(Root);

    private static int CalculateMaxDepth(BlockingChainNode node) =>
        node.Children.Count == 0 ? 0 : 1 + node.Children.Max(CalculateMaxDepth);

    private static long CalculateMaxAge(BlockingChainNode node)
    {
        var self = node.Session.TransactionAgeSeconds ?? 0;
        var childMax = node.Children.Count == 0 ? 0 : node.Children.Max(CalculateMaxAge);
        return Math.Max(self, childMax);
    }
}
