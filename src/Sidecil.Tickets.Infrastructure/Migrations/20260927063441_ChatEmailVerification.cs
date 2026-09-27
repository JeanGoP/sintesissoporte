using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerificationAttempts",
                schema: "chat",
                table: "Conversations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationExpiresAt",
                schema: "chat",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationHash",
                schema: "chat",
                table: "Conversations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationSentAt",
                schema: "chat",
                table: "Conversations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationAttempts",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "VerificationExpiresAt",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "VerificationHash",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "VerificationSentAt",
                schema: "chat",
                table: "Conversations");
        }
    }
}
