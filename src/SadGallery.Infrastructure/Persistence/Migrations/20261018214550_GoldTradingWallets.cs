using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadGallery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoldTradingWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoldTradingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    FeePercent = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldTradingSettings", x => x.Id);
                    table.CheckConstraint("CK_GoldSettings_Fee", "([FeePercent] IS NULL AND [Enabled] = 0) OR ([FeePercent] IS NOT NULL AND [FeePercent] BETWEEN 0 AND 100)");
                    table.CheckConstraint("CK_GoldSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "GoldWallets",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CashIrt = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    ReservedIrt = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Grams750 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldWallets", x => x.UserId);
                    table.CheckConstraint("CK_GoldWallet_Balances", "[CashIrt] >= 0 AND [CashIrt] <= 999999999999999 AND [ReservedIrt] >= 0 AND [ReservedIrt] <= [CashIrt] AND [Grams750] >= 0 AND [Grams750] <= 999999");
                    table.ForeignKey(
                        name: "FK_GoldWallets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoldOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RateSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Grams750 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RateIrt = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    FeePercent = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PrincipalIrt = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    FeeIrt = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    TotalIrt = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Fineness = table.Column<int>(type: "int", nullable: false),
                    FormulaVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QuotedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewerId = table.Column<int>(type: "int", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldOrders", x => x.Id);
                    table.CheckConstraint("CK_GoldOrder_Amounts", "[Grams750] > 0 AND [Grams750] <= 999999 AND [RateIrt] > 0 AND [RateIrt] <= 999999999999999 AND [FeePercent] BETWEEN 0 AND 100 AND [PrincipalIrt] > 0 AND [FeeIrt] >= 0 AND [TotalIrt] = [PrincipalIrt] + [FeeIrt] AND [TotalIrt] <= 999999999999999 AND [Fineness] = 750");
                    table.CheckConstraint("CK_GoldOrder_Status", "([Status] = 0 AND [RequestedAtUtc] IS NULL AND [ReviewedAtUtc] IS NULL AND [ReviewerId] IS NULL) OR ([Status] = 1 AND [RequestedAtUtc] IS NOT NULL AND [ReviewedAtUtc] IS NULL AND [ReviewerId] IS NULL) OR ([Status] IN (2,3,4) AND [RequestedAtUtc] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewerId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GoldOrders_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoldOrders_GoldWallets_UserId",
                        column: x => x.UserId,
                        principalTable: "GoldWallets",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoldOrders_MarketRates_RateSnapshotId",
                        column: x => x.RateSnapshotId,
                        principalTable: "MarketRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoldLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CashDeltaIrt = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    GoldDeltaGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldLedger", x => x.Id);
                    table.CheckConstraint("CK_GoldLedger_Kind", "([Kind] = 1 AND [CashDeltaIrt] > 0 AND [GoldDeltaGrams] = 0 AND [OrderId] IS NULL) OR ([Kind] = 2 AND [CashDeltaIrt] = 0 AND [GoldDeltaGrams] > 0 AND [OrderId] IS NULL) OR ([Kind] = 3 AND [CashDeltaIrt] < 0 AND [GoldDeltaGrams] > 0 AND [OrderId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GoldLedger_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoldLedger_GoldOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "GoldOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoldLedger_GoldWallets_UserId",
                        column: x => x.UserId,
                        principalTable: "GoldWallets",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "GoldTradingSettings",
                columns: new[] { "Id", "Enabled", "FeePercent" },
                values: new object[] { 1, false, null });

            migrationBuilder.CreateIndex(
                name: "IX_GoldLedger_ActorId",
                table: "GoldLedger",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLedger_OrderId",
                table: "GoldLedger",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLedger_UserId_AtUtc",
                table: "GoldLedger",
                columns: new[] { "UserId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldOrders_RateSnapshotId",
                table: "GoldOrders",
                column: "RateSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldOrders_ReviewerId",
                table: "GoldOrders",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldOrders_Status_RequestedAtUtc",
                table: "GoldOrders",
                columns: new[] { "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldOrders_UserId_RequestedAtUtc",
                table: "GoldOrders",
                columns: new[] { "UserId", "RequestedAtUtc" });
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [TR_GoldLedger_AppendOnly] ON [GoldLedger]
                INSTEAD OF UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, ''Gold ledger is append-only'', 1;
                END');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldLedger");

            migrationBuilder.DropTable(
                name: "GoldTradingSettings");

            migrationBuilder.DropTable(
                name: "GoldOrders");

            migrationBuilder.DropTable(
                name: "GoldWallets");
        }
    }
}
