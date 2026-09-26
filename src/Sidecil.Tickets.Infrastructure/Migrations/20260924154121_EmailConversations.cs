using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communications");

            migrationBuilder.AlterColumn<string>(
                name: "RequesterId",
                schema: "tickets",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                schema: "tickets",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                schema: "tickets",
                table: "Tickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCustomerReply",
                schema: "tickets",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "AuthorId",
                schema: "tickets",
                table: "Messages",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                schema: "tickets",
                table: "Messages",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "tickets",
                table: "Messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ActorId",
                schema: "audit",
                table: "Events",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                schema: "audit",
                table: "Events",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuestSubmissions",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestSubmissions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "tickets",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inbound",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: true),
                    Sender = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbound", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inbound_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "tickets",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MailboxCursors",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    LastUid = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbound",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbound", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Outbound_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "tickets",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "Id", "Name" },
                values: new object[] { new Guid("91563faf-00a3-49ac-b2d0-0e8c702f4d41"), "Solicitudes externas" });

            migrationBuilder.InsertData(
                schema: "communications",
                table: "Templates",
                columns: new[] { "Id", "Body", "Signature", "Subject", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1, "Hola {nombre},\n\nGracias por contactar a Sidecil. Registramos tu solicitud con el número {numero}. Nuestro equipo revisará tu caso y te acompañará hasta resolverlo.\n\nPuedes responder directamente a este correo para agregar información.\n\n{firma}", "Equipo de atención\nSidecil", "Recibimos tu solicitud: {asunto}", new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "IX_GuestSubmissions_Email_CreatedAt",
                schema: "communications",
                table: "GuestSubmissions",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestSubmissions_TicketId",
                schema: "communications",
                table: "GuestSubmissions",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestSubmissions_TokenHash",
                schema: "communications",
                table: "GuestSubmissions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inbound_MessageKey",
                schema: "communications",
                table: "Inbound",
                column: "MessageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inbound_SourceKey",
                schema: "communications",
                table: "Inbound",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inbound_TicketId",
                schema: "communications",
                table: "Inbound",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Outbound_DeduplicationKey",
                schema: "communications",
                table: "Outbound",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbound_MessageId",
                schema: "communications",
                table: "Outbound",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbound_State_NextAttemptAt",
                schema: "communications",
                table: "Outbound",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbound_TicketId",
                schema: "communications",
                table: "Outbound",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestSubmissions",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "Inbound",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "MailboxCursors",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "Outbound",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "Templates",
                schema: "communications");

            migrationBuilder.DeleteData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: new Guid("91563faf-00a3-49ac-b2d0-0e8c702f4d41"));

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "GuestName",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "HasCustomerReply",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                schema: "tickets",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "tickets",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ActorName",
                schema: "audit",
                table: "Events");

            migrationBuilder.AlterColumn<string>(
                name: "RequesterId",
                schema: "tickets",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AuthorId",
                schema: "tickets",
                table: "Messages",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActorId",
                schema: "audit",
                table: "Events",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
