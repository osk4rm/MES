using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKanbanLoopsAndCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KanbanLoops",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumingMachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyingWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CardsInCirculation = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanbanLoops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KanbanCards",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoopId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanbanCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanbanCards_KanbanLoops_LoopId",
                        column: x => x.LoopId,
                        principalSchema: "production",
                        principalTable: "KanbanLoops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_LoopId",
                schema: "production",
                table: "KanbanCards",
                column: "LoopId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_TenantId",
                schema: "production",
                table: "KanbanCards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_TenantId_LoopId_CardNumber",
                schema: "production",
                table: "KanbanCards",
                columns: new[] { "TenantId", "LoopId", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_TenantId_LoopId_Status",
                schema: "production",
                table: "KanbanCards",
                columns: new[] { "TenantId", "LoopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KanbanLoops_TenantId",
                schema: "production",
                table: "KanbanLoops",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanLoops_TenantId_Code",
                schema: "production",
                table: "KanbanLoops",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanbanLoops_TenantId_ProductId",
                schema: "production",
                table: "KanbanLoops",
                columns: new[] { "TenantId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KanbanCards",
                schema: "production");

            migrationBuilder.DropTable(
                name: "KanbanLoops",
                schema: "production");
        }
    }
}
