using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpcCharacteristics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpcCharacteristics",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChartType = table.Column<short>(type: "smallint", nullable: false),
                    NominalValue = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    LowerSpecLimit = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    UpperSpecLimit = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    LowerControlLimit = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    UpperControlLimit = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpcCharacteristics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpcCharacteristics_TenantId_Code",
                schema: "production",
                table: "SpcCharacteristics",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpcCharacteristics_TenantId_IsActive",
                schema: "production",
                table: "SpcCharacteristics",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpcCharacteristics",
                schema: "production");
        }
    }
}
