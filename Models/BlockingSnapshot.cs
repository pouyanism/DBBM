namespace BlockingMonitor.Models;

/// <summary>
/// خروجی نهایی هر Poll: یک عکس لحظه‌ای از تمام زنجیره‌های فعال بلاکینگ روی سرور جاری.
/// این دقیقاً همون شیءای هست که Endpoint برمی‌گردونه و بعداً از طریق SignalR هم Push میشه
/// (بدون هیچ تغییری در ساختار - فقط کانال توزیع عوض میشه).
/// </summary>
public sealed class BlockingSnapshot
{
    public required DateTime CapturedAtUtc { get; init; }
    public required string ServerName { get; init; }
    public List<BlockingChain> ActiveChains { get; init; } = [];
    public bool HasActiveBlocking => ActiveChains.Count > 0;
}
