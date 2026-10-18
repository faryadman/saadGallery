using System.Globalization;
using System.Text;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Administration;

public sealed record AuditActor(int UserId, string? IpAddress, string CorrelationId);
public interface IAuditContext { AuditActor? Actor { get; } }
public sealed record Page<T>(IReadOnlyList<T> Items, int Number, int Total, int Size = 25)
{
    public bool HasNext => (long)Number * Size < Total;
}
public sealed record ManagedUser(int Id, string DisplayName, bool IsActive, string Role, string Version, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastLoginAtUtc);
public sealed record RateHealth(string AssetCode, DateTimeOffset QuotedAtUtc, DateTimeOffset FetchedAtUtc, RateQuality Quality, bool Flagged);
public sealed record JobHealth(DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc, int Outcome, int Accepted, int Flagged, int Rejected);
public sealed record OperationsDashboard(int OpenTickets, int AwaitingCustomer, int PublishedProducts, int DraftProducts,
    string Source, bool Configured, bool Paused, DateTimeOffset? LastFetchUtc, JobHealth? LastJob, IReadOnlyList<RateHealth> Rates)
{
    public string HealthLabel => !Configured ? "دریافت غیرفعال / پیکربندی نشده" : Paused ? "دریافت متوقف است" : LastJob == null || Rates.Count == 0 ? "داده کافی برای ارزیابی نیست"
        : LastJob.FinishedAtUtc == null ? "اجرای ناتمام؛ بررسی لازم" : LastJob.Outcome is not (1 or 2) ? "آخرین دریافت ناموفق است"
        : Rates.Any(x => x.Flagged || x.Quality is RateQuality.Stale or RateQuality.Invalid) ? "نرخ‌های نیازمند بررسی" : "آخرین دریافت موفق است";
}
public sealed record RuntimeRateSettings(bool Paused, int IntervalSeconds, byte[] Version, DateTimeOffset? UpdatedAtUtc, int? UpdatedByUserId);
public sealed record AuditRow(long Id, DateTimeOffset AtUtc, int? ActorId, string Action, string EntityType, string EntityId, string Fields, string? IpAddress, string CorrelationId);
public enum AdminSaveResult { Saved, Unchanged, Invalid, Conflict, Forbidden, NotFound, SelfChange, LastAdmin }
public interface IRuntimeRateSettings
{
    Task<RuntimeRateSettings?> GetAsync(CancellationToken ct);
}
public interface IAdministrationStore : IRuntimeRateSettings
{
    Task<OperationsDashboard> DashboardAsync(CancellationToken ct);
    Task<Page<ManagedUser>> UsersAsync(int page, string? search, CancellationToken ct);
    Task<ManagedUser?> UserAsync(int id, CancellationToken ct);
    Task<AdminSaveResult> SaveUserAsync(int actorId, int id, string role, bool active, string version, CancellationToken ct);
    Task<AdminSaveResult> SaveSettingsAsync(int actorId, bool paused, int interval, byte[] version, CancellationToken ct);
    Task<Page<AuditRow>> AuditAsync(int page, CancellationToken ct);
    Task<IReadOnlyList<AuditRow>> AuditExportAsync(CancellationToken ct);
}
public static class AdminRules
{
    public static string ActionTitle(string action) => action switch
    {
        "Gold.SettingsChanged" => "تنظیم کارمزد و خرید طلا", "Gold.WalletChanged" => "تغییر کیف مشتری ویژه",
        "Gold.OrderChanged" => "تغییر وضعیت خرید طلا", "Gold.LedgerPosted" => "ثبت قطعی دفتر کیف طلا",
        "Settings.Changed" => "تغییر تنظیمات نرخ", "ConnectionNotice.Changed" => "ویرایش متن اعلان اتصال",
        "User.AccessChanged" => "تغییر وضعیت حساب", "User.RoleChanged" => "تغییر نقش کاربر",
        "Product.Deleted" => "حذف کالا", "Product.Changed" => "ویرایش کالا", "ProductImage.Deleted" => "حذف تصویر کالا",
        "Category.Changed" => "ویرایش گروه", "Ticket.StatusChanged" => "تغییر وضعیت تیکت", "Ticket.AssignmentChanged" => "تغییر مسئول تیکت",
        _ => "رویداد مدیریت"
    };
    public static AdminSaveResult? AccessChangeGuard(int actorId, int targetId, bool currentlyActive, bool currentlyAdmin, bool active, string role, int activeAdminCount)
    {
        if (actorId == targetId) return AdminSaveResult.SelfChange;
        if (currentlyActive && currentlyAdmin && (!active || role != "Admin") && activeAdminCount <= 1) return AdminSaveResult.LastAdmin;
        return null;
    }
    public static bool ValidRole(string role) => role is "Customer" or "Operator" or "Admin";
    public static int PageNumber(int page) => Math.Clamp(page, 1, 100_000);
    public static bool ValidInterval(int interval, int configuredMinimum) => interval >= Math.Max(10, configuredMinimum) && interval <= 3600;
    public static string RoleTitle(string role) => role switch { "Admin" => "مدیر", "Operator" => "اپراتور", "Customer" => "مشتری", _ => "چند نقش / بررسی لازم" };
    public static string SourceTitle(string source) => source.ToLowerInvariant() switch { "tgn" => "منبع نرخ پیکربندی‌شده", "fixture" => "نمونه آزمایشی؛ نرخ روز بازار نیست", _ => "غیرفعال" };
}
public static class AdminCsv
{
    // Spreadsheet programs also recognize formulas after whitespace and control characters.
    public static string Cell(string? value)
    {
        value ??= "";
        var first = value.SkipWhile(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\uFEFF').FirstOrDefault();
        if (first is '=' or '+' or '-' or '@' || value.Any(c => c is '\t' or '\r' or '\n')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
    public static byte[] Audit(IReadOnlyList<AuditRow> rows)
    {
        var text = new StringBuilder("شناسه,زمان UTC,عامل,عملیات,نوع,شناسه هدف,فیلدها\r\n");
        foreach (var x in rows.Take(500))
            text.AppendLine(string.Join(',', new[] { x.Id.ToString(CultureInfo.InvariantCulture), x.AtUtc.ToString("O", CultureInfo.InvariantCulture), x.ActorId?.ToString(CultureInfo.InvariantCulture), x.Action, x.EntityType, x.EntityId, x.Fields }.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }
}
