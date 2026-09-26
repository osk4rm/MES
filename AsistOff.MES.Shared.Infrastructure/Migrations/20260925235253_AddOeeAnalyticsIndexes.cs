using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOeeAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProductionConfirmations_TenantId_MachineId_ReportedAt",
                schema: "production",
                table: "ProductionConfirmations",
                columns: new[] { "TenantId", "MachineId", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationNodes_TenantId_RecipeVersionId",
                schema: "production",
                table: "OperationNodes",
                columns: new[] { "TenantId", "RecipeVersionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionConfirmations_TenantId_MachineId_ReportedAt",
                schema: "production",
                table: "ProductionConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_OperationNodes_TenantId_RecipeVersionId",
                schema: "production",
                table: "OperationNodes");
        }
    }
}
