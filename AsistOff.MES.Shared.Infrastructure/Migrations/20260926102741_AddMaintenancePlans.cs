using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenancePlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                schema: "config",
                table: "MaintenanceWorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenancePlans",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerType = table.Column<short>(type: "smallint", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: true),
                    MeterIntervalValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    NextDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenancePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenancePlans_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "config",
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_PlanId",
                schema: "config",
                table: "MaintenanceWorkOrders",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_IsActive",
                schema: "config",
                table: "MaintenancePlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_MachineId",
                schema: "config",
                table: "MaintenancePlans",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_NextDueAt",
                schema: "config",
                table: "MaintenancePlans",
                column: "NextDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_TenantId",
                schema: "config",
                table: "MaintenancePlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_TenantId_Code",
                schema: "config",
                table: "MaintenancePlans",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceWorkOrders_MaintenancePlans_PlanId",
                schema: "config",
                table: "MaintenanceWorkOrders",
                column: "PlanId",
                principalSchema: "config",
                principalTable: "MaintenancePlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceWorkOrders_MaintenancePlans_PlanId",
                schema: "config",
                table: "MaintenanceWorkOrders");

            migrationBuilder.DropTable(
                name: "MaintenancePlans",
                schema: "config");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceWorkOrders_PlanId",
                schema: "config",
                table: "MaintenanceWorkOrders");

            migrationBuilder.DropColumn(
                name: "PlanId",
                schema: "config",
                table: "MaintenanceWorkOrders");
        }
    }
}
