using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorShiftAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorShiftAssignments",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorShiftAssignments_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "config",
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorShiftAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "config",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorShiftAssignments_Date",
                schema: "config",
                table: "OperatorShiftAssignments",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorShiftAssignments_OperatorId",
                schema: "config",
                table: "OperatorShiftAssignments",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorShiftAssignments_ShiftId",
                schema: "config",
                table: "OperatorShiftAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorShiftAssignments_TenantId",
                schema: "config",
                table: "OperatorShiftAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorShiftAssignments_TenantId_OperatorId_ShiftId_Date",
                schema: "config",
                table: "OperatorShiftAssignments",
                columns: new[] { "TenantId", "OperatorId", "ShiftId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorShiftAssignments",
                schema: "config");
        }
    }
}
