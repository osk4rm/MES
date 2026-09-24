using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionConfirmations",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GoodQuantity = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    ScrapQuantity = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionConfirmations_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "production",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionConfirmations_ProductionOrderId",
                schema: "production",
                table: "ProductionConfirmations",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionConfirmations_TenantId",
                schema: "production",
                table: "ProductionConfirmations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionConfirmations_TenantId_ProductionOrderId_Reported~",
                schema: "production",
                table: "ProductionConfirmations",
                columns: new[] { "TenantId", "ProductionOrderId", "ReportedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionConfirmations",
                schema: "production");
        }
    }
}
