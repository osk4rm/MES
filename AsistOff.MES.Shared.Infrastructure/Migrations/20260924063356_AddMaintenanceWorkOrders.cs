using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceWorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceWorkOrders",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "config",
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_MachineId",
                schema: "config",
                table: "MaintenanceWorkOrders",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_Status",
                schema: "config",
                table: "MaintenanceWorkOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_TenantId",
                schema: "config",
                table: "MaintenanceWorkOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_TenantId_Code",
                schema: "config",
                table: "MaintenanceWorkOrders",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceWorkOrders",
                schema: "config");
        }
    }
}
