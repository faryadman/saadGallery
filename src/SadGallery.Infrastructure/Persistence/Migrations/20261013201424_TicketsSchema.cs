using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadGallery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RelatedProductId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedOperatorUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FirstResponseAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_AspNetUsers_AssignedOperatorUserId",
                        column: x => x.AssignedOperatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_AspNetUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Products_RelatedProductId",
                        column: x => x.RelatedProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketInternalNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketInternalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketInternalNotes_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketInternalNotes_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    IsOperator = table.Column<bool>(type: "bit", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMessages_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedOperatorUserId_Status",
                table: "SupportTickets",
                columns: new[] { "AssignedOperatorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CreatedAtUtc",
                table: "SupportTickets",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CustomerUserId_LastActivityAtUtc",
                table: "SupportTickets",
                columns: new[] { "CustomerUserId", "LastActivityAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Number",
                table: "SupportTickets",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_RelatedProductId",
                table: "SupportTickets",
                column: "RelatedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_LastActivityAtUtc",
                table: "SupportTickets",
                columns: new[] { "Status", "LastActivityAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Subject",
                table: "SupportTickets",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEvents_CreatedAtUtc",
                table: "TicketEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEvents_TicketId_CreatedAtUtc",
                table: "TicketEvents",
                columns: new[] { "TicketId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketInternalNotes_AuthorUserId",
                table: "TicketInternalNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketInternalNotes_TicketId_CreatedAtUtc",
                table: "TicketInternalNotes",
                columns: new[] { "TicketId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_AuthorUserId",
                table: "TicketMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_CreatedAtUtc",
                table: "TicketMessages",
                columns: new[] { "TicketId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketEvents");

            migrationBuilder.DropTable(
                name: "TicketInternalNotes");

            migrationBuilder.DropTable(
                name: "TicketMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");
        }
    }
}
