using System.ComponentModel.DataAnnotations;
using SadGallery.Application.Trading;
using SadGallery.Domain.Trading;
namespace SadGallery.Web.Models;

public sealed class GoldBuyForm
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] public Guid OperationId { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage = "وزن خرید را وارد کنید."), StringLength(40)] public string Weight { get; set; } = "";
}
public sealed class GoldCreditForm
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] public Guid OperationId { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage = "نوع شارژ را انتخاب کنید.")] public GoldLedgerKind? Kind { get; set; }
    [Required(ErrorMessage = "مقدار شارژ را وارد کنید."), StringLength(40)] public string Amount { get; set; } = "";
    [Required(ErrorMessage = "دلیل شارژ را بنویسید."), StringLength(300, MinimumLength = 3, ErrorMessage = "توضیح باید ۳ تا ۳۰۰ نویسه باشد.")] public string Reason { get; set; } = "";
}
public class GoldVersionForm
{
    [StringLength(16)] public string Version { get; set; } = "";
    public byte[]? Bytes() { try { var b = Convert.FromBase64String(Version ?? ""); return b.Length == 8 ? b : null; } catch (FormatException) { return null; } }
}
public sealed class GoldSettingsForm : GoldVersionForm
{
    public bool Enabled { get; set; }
    [Required(ErrorMessage = "درصد کارمزد را وارد کنید؛ صفر هم مجاز است."), StringLength(40)] public string Fee { get; set; } = "";
}
public sealed class GoldEnableForm : GoldVersionForm { public bool Enabled { get; set; } }
public sealed class GoldReviewForm : GoldVersionForm
{
    [Required(ErrorMessage = "تأیید یا رد را انتخاب کنید.")] public GoldOrderStatus? Decision { get; set; }
    [StringLength(300, ErrorMessage = "توضیح حداکثر ۳۰۰ نویسه باشد.")] public string? Note { get; set; }
}
public sealed record GoldCustomerModel(GoldWalletPage Data, GoldBuyForm Buy);
public sealed record GoldAdminWalletModel(int UserId, string Name, GoldWalletPage Data, GoldCreditForm Credit);
public sealed record GoldOrderModel(GoldOrderView Order, bool Staff, GoldReviewForm Review, DateTimeOffset Now);
