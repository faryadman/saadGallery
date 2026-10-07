using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadGallery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketFetchLease",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketFetchLease", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketRateFetchRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    AcceptedCount = table.Column<int>(type: "int", nullable: false),
                    FlaggedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRateFetchRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuoteUnit = table.Column<int>(type: "int", nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    QuotedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Quality = table.Column<int>(type: "int", nullable: false),
                    ProviderRawValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ScaleApplied = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsAnomalySuspected = table.Column<bool>(type: "bit", nullable: false),
                    FetchRunId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketRates_MarketRateFetchRuns_FetchRunId",
                        column: x => x.FetchRunId,
                        principalTable: "MarketRateFetchRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "MarketFetchLease",
                columns: new[] { "Id", "AcquiredAtUtc", "ExpiresAtUtc", "OwnerId" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRateFetchRuns_StartedAtUtc",
                table: "MarketRateFetchRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MarketRates_AssetCode_Id",
                table: "MarketRates",
                columns: new[] { "AssetCode", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRates_FetchedAtUtc",
                table: "MarketRates",
                column: "FetchedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MarketRates_FetchRunId",
                table: "MarketRates",
                column: "FetchRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketFetchLease");

            migrationBuilder.DropTable(
                name: "MarketRates");

            migrationBuilder.DropTable(
                name: "MarketRateFetchRuns");
        }
    }
}
