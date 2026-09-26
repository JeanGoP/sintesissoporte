using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatExternalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityIssuer",
                schema: "chat",
                table: "Conversations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityProvider",
                schema: "chat",
                table: "Conversations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentitySubject",
                schema: "chat",
                table: "Conversations",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdentityVerifiedAt",
                schema: "chat",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginAttempts_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "chat",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_ConversationId",
                schema: "chat",
                table: "LoginAttempts",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_ExpiresAt",
                schema: "chat",
                table: "LoginAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_KeyHash",
                schema: "chat",
                table: "LoginAttempts",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAttempts",
                schema: "chat");

            migrationBuilder.DropColumn(
                name: "IdentityIssuer",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IdentityProvider",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IdentitySubject",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IdentityVerifiedAt",
                schema: "chat",
                table: "Conversations");
        }
    }
}
