using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TicketReadVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketReads",
                schema: "tickets",
                columns: table => new
                {
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastEventId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketReads", x => new { x.TicketId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TicketReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketReads_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "tickets",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketReads_UserId",
                schema: "tickets",
                table: "TicketReads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketReads",
                schema: "tickets");
        }
    }
}
