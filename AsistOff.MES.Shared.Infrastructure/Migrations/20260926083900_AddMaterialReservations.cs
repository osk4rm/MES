using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterialReservations",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityReserved = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    QuantityRelieved = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialReservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_TenantId",
                schema: "config",
                table: "MaterialReservations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_TenantId_ProductionOrderId",
                schema: "config",
                table: "MaterialReservations",
                columns: new[] { "TenantId", "ProductionOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_TenantId_ProductionOrderId_ProductId_W~",
                schema: "config",
                table: "MaterialReservations",
                columns: new[] { "TenantId", "ProductionOrderId", "ProductId", "WarehouseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialReservations",
                schema: "config");
        }
    }
}
