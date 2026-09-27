using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sidecil.Tickets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportCatalogAndAgentModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "tickets",
                table: "Tickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                schema: "tickets",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "communications",
                table: "GuestSubmissions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                schema: "communications",
                table: "GuestSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "chat",
                table: "Conversations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                schema: "chat",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserOrganizations",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOrganizations", x => new { x.UserId, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_UserOrganizations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserOrganizations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                schema: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Modules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "tickets",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentModules",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentModules", x => new { x.UserId, x.ModuleId });
                    table.ForeignKey(
                        name: "FK_AgentModules_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentModules_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "tickets",
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "tickets",
                table: "Categories",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[,]
                {
                    { new Guid("c1000000-0000-0000-0000-000000000001"), true, "General" },
                    { new Guid("c1000000-0000-0000-0000-000000000002"), true, "Soporte técnico" },
                    { new Guid("c1000000-0000-0000-0000-000000000003"), true, "Facturación" },
                    { new Guid("c1000000-0000-0000-0000-000000000004"), true, "Accesos" },
                    { new Guid("c1000000-0000-0000-0000-000000000005"), true, "Servicios" }
                });

            migrationBuilder.InsertData(
                schema: "tickets",
                table: "Modules",
                columns: new[] { "Id", "CategoryId", "Enabled", "Name" },
                values: new object[,]
                {
                    { new Guid("d1000000-0000-0000-0000-000000000001"), new Guid("c1000000-0000-0000-0000-000000000001"), true, "General" },
                    { new Guid("d1000000-0000-0000-0000-000000000002"), new Guid("c1000000-0000-0000-0000-000000000002"), true, "General" },
                    { new Guid("d1000000-0000-0000-0000-000000000003"), new Guid("c1000000-0000-0000-0000-000000000003"), true, "General" },
                    { new Guid("d1000000-0000-0000-0000-000000000004"), new Guid("c1000000-0000-0000-0000-000000000004"), true, "General" },
                    { new Guid("d1000000-0000-0000-0000-000000000005"), new Guid("c1000000-0000-0000-0000-000000000005"), true, "General" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ModuleId",
                schema: "tickets",
                table: "Tickets",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestSubmissions_ModuleId",
                schema: "communications",
                table: "GuestSubmissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ModuleId",
                schema: "chat",
                table: "Conversations",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentModules_ModuleId",
                schema: "identity",
                table: "AgentModules",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                schema: "tickets",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modules_CategoryId_Name",
                schema: "tickets",
                table: "Modules",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizations_OrganizationId",
                schema: "identity",
                table: "UserOrganizations",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Modules_ModuleId",
                schema: "chat",
                table: "Conversations",
                column: "ModuleId",
                principalSchema: "tickets",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestSubmissions_Modules_ModuleId",
                schema: "communications",
                table: "GuestSubmissions",
                column: "ModuleId",
                principalSchema: "tickets",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Modules_ModuleId",
                schema: "tickets",
                table: "Tickets",
                column: "ModuleId",
                principalSchema: "tickets",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Modules_ModuleId",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestSubmissions_Modules_ModuleId",
                schema: "communications",
                table: "GuestSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Modules_ModuleId",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "AgentModules",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserOrganizations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Modules",
                schema: "tickets");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ModuleId",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_GuestSubmissions_ModuleId",
                schema: "communications",
                table: "GuestSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ModuleId",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                schema: "tickets",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "communications",
                table: "GuestSubmissions");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                schema: "communications",
                table: "GuestSubmissions");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                schema: "chat",
                table: "Conversations");
        }
    }
}
