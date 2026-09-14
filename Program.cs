using BlockingMonitor.Abstractions;
using BlockingMonitor.Services;
using BlockingMonitor.Hubs;


var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "BlockingMonitorSvc";
});

// --- ثبت سرویس‌ها ---
// نکته‌ی مهم: تنها خطی که هنگام ارتقا به SignalR در آینده تغییر می‌کنه همینه:
// IBlockingSnapshotPublisher رو از InMemorySnapshotPublisher به SignalRSnapshotPublisher
// عوض می‌کنیم؛ هیچ کلاس دیگه‌ای دست‌نخورده باقی می‌مونه.
builder.Services.AddSingleton<InMemorySnapshotPublisher>();
builder.Services.AddSingleton<IBlockingSnapshotPublisher, SignalRSnapshotPublisher>();
//builder.Services.AddSingleton<IBlockingSnapshotPublisher>(sp => sp.GetRequiredService<InMemorySnapshotPublisher>());

/*Connection String Builder*/
builder.Services.AddSingleton<IActiveServerProvider, ActiveServerProvider>();
builder.Services.AddSingleton<ConnectionStringBuilder>();
builder.Services.AddSingleton<DatabaseConnectionService>();


// نکته‌ی مهم: باید Singleton باشه، نه Scoped.
// PollerService (که یک BackgroundService/Singleton است) این سرویس رو Inject می‌کنه،
// و یک Singleton نمی‌تونه به یک Scoped وابسته باشه (خطای Captive Dependency).
// این بی‌خطره چون SqlBlockingDataReader هیچ State ای نگه نمی‌داره؛ هر Poll خودش
// یک SqlConnection جدید باز و await using می‌کنه و می‌بندتش.
builder.Services.AddSingleton<IBlockingDataReader, SqlBlockingDataReader>();
builder.Services.AddSingleton<ChainTracker>();
builder.Services.AddHostedService<PollerService>();

// CORS ساده برای MVP؛ در Production باید به Origin دقیق مانیتور بزرگ محدود بشه
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        //.WithOrigins("https://amar.abidipharma.com/api")
        .AllowAnyMethod()
        .AllowAnyHeader());
});

builder.Services.AddSignalR();
builder.Services.AddControllers();

var app = builder.Build();

app.UseCors();
app.MapHub<BlockingHub>("/hubs/blocking");
app.MapControllers();

// --- Endpoint اصلی MVP ---
// فرانت هر 2 ثانیه این آدرس رو صدا می‌زنه (Simple Polling سمت کلاینت).
app.MapGet("/api/snapshot/current", (InMemorySnapshotPublisher publisher) =>
{
    var snapshot = publisher.GetLatest();

    return snapshot is null
        ? Results.Ok(new
        {
            capturedAtUtc = DateTime.UtcNow,
            serverName = "",
            activeChains = Array.Empty<object>(),
            hasActiveBlocking = false,
            note = "هنوز اولین Poll انجام نشده؛ چند لحظه صبر کنید."
        })
        : Results.Ok(snapshot);
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

// صرفاً برای راحتی وقتی کسی مستقیم ریشه‌ی سایت رو در مرورگر باز می‌کنه
app.MapGet("/", () => Results.Ok(new
{
    message = "BlockingMonitor API در حال اجراست.",
    endpoints = new[] { "/api/health", "/api/snapshot/current", "/hubs/blocking" }
}));

app.Run();
