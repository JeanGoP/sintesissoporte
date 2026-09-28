using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PublicGuestAccessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerificationAttempts",
                schema: "communications",
                table: "PublicGuestAccesses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationExpiresAt",
                schema: "communications",
                table: "PublicGuestAccesses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationHash",
                schema: "communications",
                table: "PublicGuestAccesses",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                schema: "communications",
                table: "PublicGuestAccesses",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationAttempts",
                schema: "communications",
                table: "PublicGuestAccesses");

            migrationBuilder.DropColumn(
                name: "VerificationExpiresAt",
                schema: "communications",
                table: "PublicGuestAccesses");

            migrationBuilder.DropColumn(
                name: "VerificationHash",
                schema: "communications",
                table: "PublicGuestAccesses");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                schema: "communications",
                table: "PublicGuestAccesses");
        }
    }
}
