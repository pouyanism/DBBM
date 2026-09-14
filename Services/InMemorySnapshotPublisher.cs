using BlockingMonitor.Abstractions;
using BlockingMonitor.Models;

namespace BlockingMonitor.Services;

public sealed class InMemorySnapshotPublisher : IBlockingSnapshotPublisher
{
    // volatile کافیه چون فقط reference رو عوض می‌کنیم، نه محتوای داخلی شیء را mutate می‌کنیم.
    // نویسنده (PollerService، یک Thread) و خواننده‌ها (HTTP requestها، چند Thread) هیچ‌وقت
    // با هم روی یک instance تداخل نمی‌کنن چون BlockingSnapshot immutable-به-اندازه-کافیه.
    private volatile BlockingSnapshot? _latest;

    public void Publish(BlockingSnapshot snapshot) => _latest = snapshot;

    public BlockingSnapshot? GetLatest() => _latest;
    public void Clear() => _latest = null;
}
