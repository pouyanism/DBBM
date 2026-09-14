# BlockingMonitor — MVP

## پیش‌نیاز روی SQL Server

لاگین مانیتورینگ فقط به این Permission نیاز داره (نه sysadmin):

```sql
CREATE LOGIN [BlockingMonitorSvc] WITH PASSWORD = '...';
CREATE USER [BlockingMonitorSvc] FOR LOGIN [BlockingMonitorSvc];
GRANT VIEW SERVER STATE TO [BlockingMonitorSvc];
```

`VIEW SERVER STATE` برای خوندن `dm_os_waiting_tasks`, `dm_exec_requests`,
`dm_exec_sessions`, `dm_tran_locks` و بقیه‌ی DMVهایی که کوئری استفاده می‌کنه کافیه.

## راه‌اندازی

1. `ConnectionStrings:MonitoredServer` رو در `appsettings.Development.json`
   (یا بهتر: `dotnet user-secrets`) ست کن — **هرگز پسورد واقعی رو در
   appsettings.json کامیت نکن.**

   ```
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:MonitoredServer" "Server=...;..."
   ```

2. اجرا:
   ```
   dotnet restore
   dotnet run
   ```

3. تست:
   ```
   curl http://localhost:5000/api/snapshot/current
   ```
   هر ۲ ثانیه سرور پول میشه؛ اگه بلاکینگ فعالی نباشه `activeChains: []` برمی‌گرده.

## معماری در یک نگاه

```
SQL Server  --(هر 2 ثانیه)-->  SqlBlockingDataReader
                                       |
                                  List<SessionRow>  (Flat، از Sql/BlockingQuery.sql)
                                       |
                                  ChainBuilder  (فقط Group/Nest، بدون منطق جدید)
                                       |
                                  ChainTracker  (Dedup + State بین Pollها)
                                       |
                                  BlockingSnapshot
                                       |
                          IBlockingSnapshotPublisher
                                       |
                        InMemorySnapshotPublisher (MVP)
                                       |
                          GET /api/snapshot/current  <-- فرانت هر 2s صدا می‌زنه
```

## نکته‌ی مهم برای تغییرات منطقی

هر تغییری در **منطق تشخیص بلاکینگ** (کدوم wait typeها، threshold عمق، ستون‌های
اضافه) فقط در `Sql/BlockingQuery.sql` انجام میشه. کد C# فقط نتیجه رو Flat
می‌خونه و بر اساس `parent_session_id` / `head_blocker_session_id` که خود
کوئری محاسبه کرده، Tree می‌سازه.

## مسیر ارتقا به SignalR (فاز بعدی، نه MVP)

فقط کافیه:
1. یک `SignalRSnapshotPublisher : IBlockingSnapshotPublisher` بسازی که به‌جای
   نگه‌داشتن آخرین مقدار، `Publish()` رو مستقیم به `IHubContext` push کنه.
2. در `Program.cs`، ثبت DI رو از `InMemorySnapshotPublisher` به
   `SignalRSnapshotPublisher` عوض کنی.

`PollerService`, `ChainTracker`, `ChainBuilder`, `SqlBlockingDataReader` —
هیچ‌کدوم دست‌نخورده باقی می‌مونن.

## مسیر ارتقا به چند سرور با Switch (فاز بعدی)

`IActiveServerProvider` رو از یک مقدار ثابت appsettings به یک پیاده‌سازی
Thread-safe با متد `SwitchTo(serverId)` تبدیل کن. چون `PollerService` همیشه
از طریق این Interface به Connection String دسترسی داره، خودِ Poller نیازی
به تغییر نداره — فقط باید حلقه‌ی Poll رو موقع Switch با یک
`CancellationTokenSource` جدید Restart کنی (طراحی‌ش قبلاً در گفتگو مشخص شد).
