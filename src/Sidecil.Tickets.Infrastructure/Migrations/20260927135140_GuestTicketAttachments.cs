using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestTicketAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestAttachments",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestAttachments", x => x.Id);
                    table.CheckConstraint("CK_GuestAttachment_Size", "[Length] > 0 AND [Length] <= 5242880 AND DATALENGTH([Content]) = [Length]");
                    table.ForeignKey(
                        name: "FK_GuestAttachments_GuestSubmissions_GuestSubmissionId",
                        column: x => x.GuestSubmissionId,
                        principalSchema: "communications",
                        principalTable: "GuestSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestAttachments_GuestSubmissionId",
                schema: "communications",
                table: "GuestAttachments",
                column: "GuestSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestAttachments",
                schema: "communications");
        }
    }
}
