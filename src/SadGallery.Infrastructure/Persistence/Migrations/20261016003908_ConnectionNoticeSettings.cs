using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadGallery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionNoticeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionNoticeEdits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Fields = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionNoticeEdits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionNoticeEdits_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionNoticeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    OfflineTitle = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OfflineDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OnlineTitle = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OnlineDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionNoticeSettings", x => x.Id);
                    table.CheckConstraint("CK_ConnectionNoticeSettings_Singleton", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_ConnectionNoticeSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ConnectionNoticeSettings",
                columns: new[] { "Id", "OfflineDescription", "OfflineTitle", "OnlineDescription", "OnlineTitle", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[] { 1, "اطلاعات صفحه به‌روز نمی‌شود؛ نرخ‌های ذخیره‌شده لحظه‌ای نیستند.", "اتصال اینترنت قطع است", "برای دریافت اطلاعات تازه، صفحه را به‌روز کنید.", "اتصال شبکه برگشت", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionNoticeEdits_ActorUserId",
                table: "ConnectionNoticeEdits",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionNoticeEdits_CreatedAtUtc",
                table: "ConnectionNoticeEdits",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionNoticeSettings_UpdatedByUserId",
                table: "ConnectionNoticeSettings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionNoticeEdits");

            migrationBuilder.DropTable(
                name: "ConnectionNoticeSettings");
        }
    }
}
