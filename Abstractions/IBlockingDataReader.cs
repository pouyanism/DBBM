using BlockingMonitor.Models;

namespace BlockingMonitor.Abstractions;

/// <summary>
/// مسئول اجرای Sql/BlockingQuery.sql روی سرور جاری و برگردوندن نتیجه‌ی خام.
/// این لایه هیچ منطقی نداره - فقط ADO.NET Read. تمام منطق تشخیص Head Blocker
/// و عمق زنجیره از قبل در خود کوئری SQL انجام شده.
/// </summary>
public interface IBlockingDataReader
{
    Task<List<SessionRow>> GetBlockingSessionsAsync(CancellationToken cancellationToken);
}
