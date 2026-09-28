using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PublicGuestAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicGuestAccesses",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicGuestAccesses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicGuestAccesses_Email_CreatedAt",
                schema: "communications",
                table: "PublicGuestAccesses",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicGuestAccesses_TokenHash",
                schema: "communications",
                table: "PublicGuestAccesses",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicGuestAccesses",
                schema: "communications");
        }
    }
}
