using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLotRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lots",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasureUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SupplierLotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProducedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_TenantId",
                schema: "production",
                table: "Lots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_TenantId_Code",
                schema: "production",
                table: "Lots",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lots_TenantId_ProductId",
                schema: "production",
                table: "Lots",
                columns: new[] { "TenantId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lots",
                schema: "production");
        }
    }
}
