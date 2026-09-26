using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftHandovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftHandovers",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    From = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    To = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OpenOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    ActiveAndonCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftHandovers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandovers_TenantId",
                schema: "production",
                table: "ShiftHandovers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandovers_TenantId_MachineId",
                schema: "production",
                table: "ShiftHandovers",
                columns: new[] { "TenantId", "MachineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandovers_TenantId_MachineId_From",
                schema: "production",
                table: "ShiftHandovers",
                columns: new[] { "TenantId", "MachineId", "From" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftHandovers",
                schema: "production");
        }
    }
}
