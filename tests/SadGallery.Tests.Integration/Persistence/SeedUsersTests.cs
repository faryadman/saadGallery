using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Identity;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Time;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Persistence;

/// <summary>
/// تست‌های Seed کاربران اولیه (ADR-0011).
/// تست‌های نیازمند SQL Server با [RequiresSqlServerFact] اجرا/سkip می‌شوند؛
/// تست «بدون رمز عبور» عمداً بدون دیتابیس است، چون باید *پیش از* هر دسترسی به دیتابیس شکست بخورد.
/// </summary>
public sealed class SeedUsersTests
{
    // رمز ساختگی مخصوص دیتابیس یک‌بارمصرف این تست است — هیچ سرویس واقعی را محافظت نمی‌کند
    // و دیتابیس در پایان تست Drop می‌شود. رمز هیچ سیستم واقعی اینجا نیست.
    private const string TestPassword = "SadTest1404Pass";

    private static SeedUserDefinition AdminDefinition() => new()
    {
        UserName = "admin@example.invalid",
        Email = "admin@example.invalid",
        DisplayName = "مدیر آزمون",
        Role = RoleNames.Admin,
        Password = TestPassword,
    };

    [Fact]
    public async Task SeedUsers_WithoutPassword_IsRejectedBeforeAnyDatabaseAccess()
    {
        // وابستگی‌ها عمداً null هستند: اگر بررسی پیش‌شرط درست کار کند، هیچ‌کدام لمس نمی‌شوند.
        var seeder = new IdentitySeeder(
            roleManager: null!,
            userManager: null!,
            clock: new SystemClock(),
            logger: NullLogger<IdentitySeeder>.Instance);

        var definitionWithoutPassword = new SeedUserDefinition
        {
            UserName = "someone@example.invalid",
            Role = RoleNames.Admin,
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedUsersAsync([definitionWithoutPassword], TestContext.Current.CancellationToken));

        Assert.Contains("someone@example.invalid", exception.Message, StringComparison.Ordinal);
        Assert.Contains(SeedUsersConfiguration.SharedPasswordEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    [RequiresSqlServerFact]
    public async Task SeedUsers_CreatesUsersWithRoles_KeepsPasswordsHashed_AndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = await TestDatabase.CreateAsync(cancellationToken);

        try
        {
            var recordingLogger = new RecordingLogger();
            await using var provider = BuildIdentityServices(database.ConnectionString, recordingLogger);

            var definitions = new[]
            {
                AdminDefinition(),
                new SeedUserDefinition
                {
                    UserName = "customer@example.invalid",
                    Email = "customer@example.invalid",
                    DisplayName = "مشتری آزمون",
                    Role = RoleNames.Customer,
                    Password = TestPassword,
                },
            };

            // ---- اجرای اول ----
            IReadOnlyList<SeedUserOutcome> firstRun;

            using (var scope = provider.CreateScope())
            {
                var seeder = CreateSeeder(scope.ServiceProvider, recordingLogger);
                await seeder.SeedAsync(cancellationToken);
                firstRun = await seeder.SeedUsersAsync(definitions, cancellationToken);
            }

            Assert.All(firstRun, outcome => Assert.True(outcome.UserCreated));
            Assert.All(firstRun, outcome => Assert.True(outcome.RoleAssigned));

            using (var scope = provider.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                var admin = await userManager.FindByNameAsync("admin@example.invalid");
                Assert.NotNull(admin);
                Assert.Equal("مدیر آزمون", admin.DisplayName);
                Assert.True(admin.IsActive);
                Assert.True(await userManager.IsInRoleAsync(admin, RoleNames.Admin));
                Assert.True(await userManager.CheckPasswordAsync(admin, TestPassword));
            }

            // رمز عبور در دیتابیس فقط به‌صورت هش است
            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
                var hashes = await dbContext.Users
                    .Select(user => user.PasswordHash)
                    .ToListAsync(cancellationToken);

                Assert.Equal(2, hashes.Count);
                Assert.All(hashes, hash =>
                {
                    Assert.NotNull(hash);
                    Assert.NotEqual(TestPassword, hash);
                    Assert.DoesNotContain(TestPassword, hash, StringComparison.Ordinal);
                });
            }

            // ---- اجرای دوم: ایدِمپوتنت، بدون تکرار و بدون تغییر رمز ----
            IReadOnlyList<SeedUserOutcome> secondRun;

            using (var scope = provider.CreateScope())
            {
                var seeder = CreateSeeder(scope.ServiceProvider, recordingLogger);
                secondRun = await seeder.SeedUsersAsync(definitions, cancellationToken);
            }

            Assert.All(secondRun, outcome => Assert.False(outcome.UserCreated));
            Assert.All(secondRun, outcome => Assert.False(outcome.RoleAssigned));

            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();

                Assert.Equal(2, await dbContext.Users.CountAsync(cancellationToken));
                Assert.Equal(2, await dbContext.UserRoles.CountAsync(cancellationToken));
                Assert.Equal(RoleNames.All.Count, await dbContext.Roles.CountAsync(cancellationToken));

                // رمز همان رمز قبلی است (بازنویسی نشده)
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var admin = await userManager.FindByNameAsync("admin@example.invalid");
                Assert.NotNull(admin);
                Assert.True(await userManager.CheckPasswordAsync(admin, TestPassword));
            }

            // ---- اصل امنیتی: رمز عبور در هیچ لاگی نیامده ----
            var logs = recordingLogger.Messages;
            Assert.NotEmpty(logs);
            Assert.DoesNotContain(logs, message => message.Contains(TestPassword, StringComparison.Ordinal));
            Assert.Contains(logs, message => message.Contains("admin@example.invalid", StringComparison.Ordinal));
        }
        finally
        {
            await database.DropAsync(cancellationToken);
        }
    }

    [RequiresSqlServerFact]
    public async Task SeedUsers_AssignsMissingRole_ToExistingUser_WithoutChangingPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = await TestDatabase.CreateAsync(cancellationToken);

        try
        {
            var recordingLogger = new RecordingLogger();
            await using var provider = BuildIdentityServices(database.ConnectionString, recordingLogger);

            // کاربر از قبل وجود دارد (مثلاً ثبت‌نام کرده) اما نقش Operator را ندارد
            using (var setupScope = provider.CreateScope())
            {
                var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var bootstrap = new IdentitySeeder(
                    setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>(),
                    userManager,
                    new SystemClock(),
                    NullLogger<IdentitySeeder>.Instance);

                await bootstrap.SeedAsync(cancellationToken);

                var existing = new ApplicationUser
                {
                    UserName = "operator@example.invalid",
                    Email = "operator@example.invalid",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    IsActive = true,
                };

                var created = await userManager.CreateAsync(existing, TestPassword);
                Assert.True(created.Succeeded);
                Assert.True((await userManager.AddToRoleAsync(existing, RoleNames.Customer)).Succeeded);
            }

            // حالا Seed با نقش بالاتر برای همان نام کاربری اجرا می‌شود
            using (var scope = provider.CreateScope())
            {
                var seeder = CreateSeeder(scope.ServiceProvider, recordingLogger);

                var outcomes = await seeder.SeedUsersAsync(
                    [
                        new SeedUserDefinition
                        {
                            UserName = "operator@example.invalid",
                            Role = RoleNames.Operator,
                            Password = TestPassword,
                        },
                    ],
                    cancellationToken);

                var outcome = Assert.Single(outcomes);
                Assert.False(outcome.UserCreated);
                Assert.True(outcome.RoleAssigned);
            }

            using (var scope = provider.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByNameAsync("operator@example.invalid");

                Assert.NotNull(user);
                Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Operator));
                Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Customer));

                // رمز کاربر موجود دست‌نخورده مانده است
                Assert.True(await userManager.CheckPasswordAsync(user, TestPassword));

                var dbContext = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
                Assert.Equal(1, await dbContext.Users.CountAsync(cancellationToken));
            }
        }
        finally
        {
            await database.DropAsync(cancellationToken);
        }
    }

    private static ServiceProvider BuildIdentityServices(string connectionString, ILogger<IdentitySeeder> logger)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SadGalleryDbContext>(builder => builder.UseSqlServer(connectionString));
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<SadGalleryDbContext>();

        return services.BuildServiceProvider();
    }

    private static IdentitySeeder CreateSeeder(IServiceProvider scope, ILogger<IdentitySeeder> logger)
        => new(
            scope.GetRequiredService<RoleManager<IdentityRole<int>>>(),
            scope.GetRequiredService<UserManager<ApplicationUser>>(),
            new SystemClock(),
            logger);

    /// <summary>لاگر ضبط‌کننده برای اثبات «رمز عبور هرگز لاگ نمی‌شود».</summary>
    private sealed class RecordingLogger : ILogger<IdentitySeeder>
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _messages.Add(formatter(state, exception));
    }

    /// <summary>
    /// دیتابیس یک‌بارمصرف تست (نام یکتا) — در پایان Drop می‌شود. هرگز روی دیتابیس واقعی اجرا نمی‌شود.
    /// </summary>
    private sealed class TestDatabase
    {
        private readonly string _masterConnectionString;
        private readonly string _databaseName;

        private TestDatabase(string masterConnectionString, string databaseName, string connectionString)
        {
            _masterConnectionString = masterConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var baseConnectionString = RequiresSqlServerFactAttribute.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(baseConnectionString));

            var databaseName = $"SadGallery_SeedTest_{Guid.NewGuid():N}";
            var masterConnectionString = BuildConnectionString(baseConnectionString!, "master");
            var connectionString = BuildConnectionString(baseConnectionString!, databaseName);

            await using (var connection = new SqlConnection(masterConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]"; // نام ساختگی خودِ آزمون (GUID)
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var options = new DbContextOptionsBuilder<SadGalleryDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using (var dbContext = new SadGalleryDbContext(options))
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }

            return new TestDatabase(masterConnectionString, databaseName, connectionString);
        }

        public async Task DropAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{_databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_databaseName}];
                END
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string BuildConnectionString(string baseConnectionString, string databaseName)
            => new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName }.ConnectionString;
    }
}
