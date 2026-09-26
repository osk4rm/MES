using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledOperations",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_TenantId",
                schema: "production",
                table: "ScheduledOperations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_TenantId_MachineId",
                schema: "production",
                table: "ScheduledOperations",
                columns: new[] { "TenantId", "MachineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_TenantId_ProductionOrderId",
                schema: "production",
                table: "ScheduledOperations",
                columns: new[] { "TenantId", "ProductionOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_TenantId_ProductionOrderId_OperationNod~",
                schema: "production",
                table: "ScheduledOperations",
                columns: new[] { "TenantId", "ProductionOrderId", "OperationNodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledOperations",
                schema: "production");
        }
    }
}
