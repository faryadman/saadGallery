using System.Net;
using SadGallery.Infrastructure.Identity;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// کنترل دسترسیِ مدیریت کالاها (فاز ۴)، با <b>درخواست مستقیمِ HTTP</b>.
/// </summary>
/// <remarks>
/// <para>
/// این تست‌ها به دیتابیس نیاز ندارند: بررسیِ مجوز پیش از هر دسترسی به داده انجام می‌شود،
/// بنابراین نتیجه در نبودِ دیتابیس هم معتبر است.
/// </para>
/// <para>
/// معیارِ پذیرش: «درخواست مستقیم HTTP توسط Customer ⇒ ۴۰۳». توجه کنید که هیچ‌کدام از این
/// درخواست‌ها از مسیرِ رابط کاربری نمی‌آیند؛ یعنی «پنهان بودنِ دکمه» در اینجا هیچ نقشی ندارد.
/// </para>
/// </remarks>
public sealed class ProductAuthorizationTests
    : IClassFixture<MemberWebFactory>, IClassFixture<SadGalleryWebFactory>
{
    private readonly MemberWebFactory _factory;
    private readonly SadGalleryWebFactory _cookieFactory;

    public ProductAuthorizationTests(MemberWebFactory factory, SadGalleryWebFactory cookieFactory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(cookieFactory);

        _factory = factory;
        _cookieFactory = cookieFactory;
    }

    [Theory]
    [InlineData("/Operator/Products")]
    [InlineData("/Operator/Products/Create")]
    [InlineData("/Operator/Products/Edit/1")]
    [InlineData("/Operator/Products/Images/1")]
    public async Task OperatorPages_ForAnonymousUser_RedirectToLogin(string path)
    {
        // نکتهٔ آموخته‌شده از فاز ۳: برای سنجشِ «تغییرمسیرِ مهمان به صفحهٔ ورود» باید از
        // کارخانه‌ای با طرحِ کوکیِ واقعی استفاده کرد؛ در کارخانهٔ MemberWebFactory چالشِ
        // طرحِ تست ۴۰۱ می‌دهد و رفتارِ واقعیِ برنامه را نشان نمی‌دهد.
        using var client = _cookieFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Operator/Products")]
    [InlineData("/Operator/Products/Create")]
    public async Task OperatorPages_ForCustomer_AreForbidden(string path)
    {
        using var client = CreateClientWithRole(RoleNames.Customer);

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Operator/Products/Delete/1")]
    [InlineData("/Operator/Products/Publish/1")]
    [InlineData("/Operator/Products/RefreshPrice/1")]
    public async Task OperatorWriteActions_ForCustomer_AreRejected(string path)
    {
        using var client = CreateClientWithRole(RoleNames.Customer);

        var response = await client.PostAsync(path, new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        // ۴۰۳ = Policy آن را رد کرده؛ ۴۰۰ = توکنِ ضدجعل ناقص بوده (باز هم اجرا نشده).
        // مهم این است که هرگز ۲xx یا تغییری در داده رخ ندهد.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.BadRequest });
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task OperatorPages_ForOperator_PassesAuthorization()
    {
        using var client = CreateClientWithRole(RoleNames.Operator);

        var response = await client.GetAsync("/Operator/Products", TestContext.Current.CancellationToken);

        // عبور از سدِ مجوز: نه ۴۰۳ و نه تغییرمسیر به ورود.
        // (بدون دیتابیس پاسخ می‌تواند ۵۰۰ باشد؛ رفتارِ درستِ آن در تست‌های
        //  SQL-gatedِ ProductCatalogSqlTests سنجیده می‌شود.)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Account/Login", response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OperatorPages_ForAdmin_PassesAuthorization()
    {
        using var client = CreateClientWithRole(RoleNames.Admin);

        var response = await client.GetAsync("/Operator/Products", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task PublicCatalog_IsOpenToEveryone()
    {
        // ویترین عمومی نباید نیازمند ورود باشد.
        using var anonymous = _cookieFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await anonymous.GetAsync("/products", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateClientWithRole(string role)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role);

        return client;
    }
}
