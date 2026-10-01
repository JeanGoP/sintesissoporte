using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplyTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReplyTemplates",
                schema: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplyTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplyTemplates_Title",
                schema: "tickets",
                table: "ReplyTemplates",
                column: "Title",
                unique: true);

            migrationBuilder.InsertData(
                schema: "tickets",
                table: "ReplyTemplates",
                columns: new[] { "Id", "Title", "Body", "Enabled", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Solicitar más información",
                      "Hola {nombre},\n\nPara avanzar con el ticket {numero} sobre {asunto}, por favor compártenos una captura de pantalla y los pasos que producen el problema.\n\nGracias,\nEquipo de soporte Sidecil", true, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Actualización del caso",
                      "Hola {nombre},\n\nSeguimos trabajando en tu ticket {numero}. Te compartiremos una actualización en cuanto tengamos nuevos resultados.\n\nGracias por tu paciencia,\nEquipo de soporte Sidecil", true, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Confirmar solución",
                      "Hola {nombre},\n\nRealizamos una corrección relacionada con el ticket {numero}. Por favor, revisa nuevamente y confírmanos si el inconveniente quedó resuelto.\n\nEquipo de soporte Sidecil", true, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplyTemplates",
                schema: "tickets");
        }
    }
}
