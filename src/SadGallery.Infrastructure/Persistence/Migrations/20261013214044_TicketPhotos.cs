using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadGallery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketPhotos_TicketMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "TicketMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketPhotos_MessageId",
                table: "TicketPhotos",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPhotos_StoredFileName",
                table: "TicketPhotos",
                column: "StoredFileName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketPhotos");
        }
    }
}
