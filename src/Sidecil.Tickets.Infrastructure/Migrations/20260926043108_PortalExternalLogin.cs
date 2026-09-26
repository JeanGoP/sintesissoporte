using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PortalExternalLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "ExternalLoginAttempts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LinkUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLoginAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLoginAttempts_AspNetUsers_LinkUserId",
                        column: x => x.LinkUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginAttempts_ExpiresAt",
                schema: "identity",
                table: "ExternalLoginAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginAttempts_KeyHash",
                schema: "identity",
                table: "ExternalLoginAttempts",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginAttempts_LinkUserId",
                schema: "identity",
                table: "ExternalLoginAttempts",
                column: "LinkUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalLoginAttempts",
                schema: "identity");
        }
    }
}
