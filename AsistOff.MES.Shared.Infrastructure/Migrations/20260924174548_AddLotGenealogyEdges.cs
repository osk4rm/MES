using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLotGenealogyEdges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotGenealogyEdges",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducedLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionConfirmationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumedQuantity = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotGenealogyEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotGenealogyEdges_Lots_ConsumedLotId",
                        column: x => x.ConsumedLotId,
                        principalSchema: "production",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotGenealogyEdges_Lots_ProducedLotId",
                        column: x => x.ProducedLotId,
                        principalSchema: "production",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotGenealogyEdges_ProductionConfirmations_ProductionConfirm~",
                        column: x => x.ProductionConfirmationId,
                        principalSchema: "production",
                        principalTable: "ProductionConfirmations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LotGenealogyEdges_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "production",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_ConsumedLotId",
                schema: "production",
                table: "LotGenealogyEdges",
                column: "ConsumedLotId");

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_ProducedLotId",
                schema: "production",
                table: "LotGenealogyEdges",
                column: "ProducedLotId");

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_ProductionConfirmationId",
                schema: "production",
                table: "LotGenealogyEdges",
                column: "ProductionConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_ProductionOrderId",
                schema: "production",
                table: "LotGenealogyEdges",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_TenantId",
                schema: "production",
                table: "LotGenealogyEdges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_TenantId_ConsumedLotId",
                schema: "production",
                table: "LotGenealogyEdges",
                columns: new[] { "TenantId", "ConsumedLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_TenantId_ProducedLotId",
                schema: "production",
                table: "LotGenealogyEdges",
                columns: new[] { "TenantId", "ProducedLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_LotGenealogyEdges_TenantId_ProductionOrderId_OccurredAt",
                schema: "production",
                table: "LotGenealogyEdges",
                columns: new[] { "TenantId", "ProductionOrderId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotGenealogyEdges",
                schema: "production");
        }
    }
}
