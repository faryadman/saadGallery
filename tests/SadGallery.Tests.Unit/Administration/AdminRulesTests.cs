using System.Text;
using SadGallery.Application.Administration;
using Xunit;
namespace SadGallery.Tests.Unit.Administration;

public sealed class AdminRulesTests
{
    [Theory]
    [InlineData("=CMD()")]
    [InlineData("\0=CMD()")]
    [InlineData("\uFEFF@SUM(1)")]
    [InlineData("  +1+1")]
    [InlineData("\t@SUM(1)")]
    [InlineData("\r-1+2")]
    [InlineData("line\n=cmd")]
    public void CsvNeutralizesFormulasAndQuotes(string input)
    { Assert.StartsWith("\"'", AdminCsv.Cell(input), StringComparison.Ordinal); Assert.Equal("\"a\"\"b\"", AdminCsv.Cell("a\"b")); }
    [Fact]
    public void ExportIsBoundedAndOmitsRequestIdentifiers()
    {
        var row = new AuditRow(1, DateTimeOffset.UtcNow, 2, "Settings.Changed", "StoreSettings", "1", "RatesPaused", "private-ip-canary", "private-trace-canary");
        var csv = Encoding.UTF8.GetString(AdminCsv.Audit(Enumerable.Repeat(row, 1000).ToArray()));
        Assert.Equal(501, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("private-ip-canary", csv, StringComparison.Ordinal); Assert.DoesNotContain("private-trace-canary", csv, StringComparison.Ordinal);
    }
    [Fact]
    public void AccessGuardPreservesSelfAndLastActiveAdministrator()
    {
        Assert.Equal(AdminSaveResult.SelfChange, AdminRules.AccessChangeGuard(1, 1, true, true, false, "Customer", 2));
        Assert.Equal(AdminSaveResult.LastAdmin, AdminRules.AccessChangeGuard(1, 2, true, true, true, "Customer", 1));
        Assert.Equal(AdminSaveResult.LastAdmin, AdminRules.AccessChangeGuard(1, 2, true, true, false, "Admin", 1));
        Assert.Null(AdminRules.AccessChangeGuard(1, 2, true, true, true, "Customer", 2));
        Assert.Null(AdminRules.AccessChangeGuard(1, 2, true, false, false, "Customer", 1));
    }
    [Theory]
    [InlineData(10, 60, false)]
    [InlineData(60, 60, true)]
    [InlineData(3600, 60, true)]
    [InlineData(3601, 60, false)]
    public void RuntimeIntervalCannotBypassDeploymentMinimum(int interval, int minimum, bool expected) => Assert.Equal(expected, AdminRules.ValidInterval(interval, minimum));
}
