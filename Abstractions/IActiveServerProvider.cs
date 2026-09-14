namespace BlockingMonitor.Abstractions;

/// <summary>
/// منبع واحد حقیقت برای "الان داریم کدوم سرور رو Poll می‌کنیم".
/// در MVP فقط یک سرور ثابت (از appsettings) برمی‌گردونه.
/// در آینده که Switch بین چند سرور اضافه بشه، همین اینترفیس یک متد
/// SwitchTo(serverId) هم می‌گیره و PollerService بدون تغییر باقی می‌مونه،
/// چون همیشه از طریق این Provider به ConnectionString/ServerName دسترسی داره.
/// </summary>
public interface IActiveServerProvider
{
    string ServerName { get; }
    string GetConnectionString();
    bool HasActiveConnection();
}
