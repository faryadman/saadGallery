using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Time;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Persistence;

/// <summary>
/// تست‌هایی که به SQL Server واقعی نیاز دارند.
/// این تست‌ها یک دیتابیس یکتا می‌سازند، Migration را اعمال می‌کنند، ایدِمپوتنت بودن Seed را
/// اثبات می‌کنند و در پایان دیتابیس را حذف می‌کنند. هرگز روی دیتابیس Production اجرا نمی‌شوند.
/// اجرا: مقدار متغیر محیطی SADGALLERY_TEST_SQL را تنظیم کنید (docs/TESTING.md).
/// </summary>
public sealed class IdentitySchemaTests
{
    [RequiresSqlServerFact]
    public async Task Migrations_ApplyToEmptyDatabase_AndRoleSeed_IsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = RequiresSqlServerFactAttribute.ConnectionString;
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString));

        var databaseName = $"SadGallery_Test_{Guid.NewGuid():N}";
        var masterConnectionString = BuildConnectionString(baseConnectionString!, "master");
        var testConnectionString = BuildConnectionString(baseConnectionString!, databaseName);

        await CreateDatabaseAsync(masterConnectionString, databaseName, cancellationToken);

        try
        {
            var options = new DbContextOptionsBuilder<SadGalleryDbContext>()
                .UseSqlServer(testConnectionString)
                .Options;

            await using (var dbContext = new SadGalleryDbContext(options))
            {
                await dbContext.Database.MigrateAsync(cancellationToken);

                var tables = await dbContext.Database
                    .SqlQueryRaw<string>("SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'")
                    .ToListAsync(cancellationToken);

                Assert.Contains("AspNetUsers", tables);
                Assert.Contains("AspNetRoles", tables);
                Assert.Contains("AspNetUserRoles", tables);
            }

            // اجرای دو بار Seed ⇒ اثبات ایدِمپوتنت بودن (بدون خطا و بدون داده تکراری)
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<SadGalleryDbContext>(builder => builder.UseSqlServer(testConnectionString));
            services
                .AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<int>>()
                .AddEntityFrameworkStores<SadGalleryDbContext>();

            await using var provider = services.BuildServiceProvider();

            for (var run = 1; run <= 2; run++)
            {
                using var scope = provider.CreateScope();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var seeder = new IdentitySeeder(
                    roleManager,
                    userManager,
                    new SystemClock(),
                    NullLogger<IdentitySeeder>.Instance);

                await seeder.SeedAsync(cancellationToken);
            }

            using (var verificationScope = provider.CreateScope())
            {
                var dbContext = verificationScope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
                var roleNames = await dbContext.Roles.Select(role => role.Name).ToListAsync(cancellationToken);

                Assert.Equal(RoleNames.All.Count, roleNames.Count);

                // طبق ADR-0011: بدون تعریف SeedUsers، هیچ کاربری ساخته نمی‌شود (نه رمز پیش‌فرض، نه کاربر ناخواسته)
                Assert.Equal(0, await dbContext.Users.CountAsync(cancellationToken));
                Assert.Contains(RoleNames.Customer, roleNames);
                Assert.Contains(RoleNames.Operator, roleNames);
                Assert.Contains(RoleNames.Admin, roleNames);
            }
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName, cancellationToken);
        }
    }

    private static string BuildConnectionString(string baseConnectionString, string databaseName)
        => new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName }.ConnectionString;

    private static async Task CreateDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // نام دیتابیس تولیدشده توسط خود آزمون است (GUID)، نه ورودی کاربر ⇒ ریسک تزریق ندارد.
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
