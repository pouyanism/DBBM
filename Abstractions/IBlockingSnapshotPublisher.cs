using BlockingMonitor.Models;

namespace BlockingMonitor.Abstractions;

/// <summary>
/// مسئول توزیع آخرین Snapshot به هر کانالی که فرانت ازش می‌خونه.
/// نسخه‌ی MVP (InMemorySnapshotPublisher) فقط آخرین نتیجه رو نگه می‌داره تا یک
/// HTTP endpoint بخونتش. نسخه‌ی آینده (SignalRSnapshotPublisher) دقیقاً همین
/// اینترفیس رو با Push آنی به کلاینت‌ها پیاده‌سازی می‌کنه.
///
/// نکته‌ی مهم: PollerService و ChainTracker هیچ وابستگی به نوع پیاده‌سازی ندارن.
/// جایگزینی فقط در ثبت DI (Program.cs) انجام میشه.
/// </summary>
public interface IBlockingSnapshotPublisher
{
    void Publish(BlockingSnapshot snapshot);
}
