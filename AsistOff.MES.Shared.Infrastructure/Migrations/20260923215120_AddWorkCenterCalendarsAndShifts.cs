using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCenterCalendarsAndShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shifts",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkCenterCalendars",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCenterCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkCenterCalendars_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "config",
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkCenterCalendarEntries",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCenterCalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<short>(type: "smallint", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsWorking = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCenterCalendarEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkCenterCalendarEntries_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "config",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkCenterCalendarEntries_WorkCenterCalendars_WorkCenterCal~",
                        column: x => x.WorkCenterCalendarId,
                        principalSchema: "config",
                        principalTable: "WorkCenterCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId",
                schema: "config",
                table: "Shifts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId_Code",
                schema: "config",
                table: "Shifts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterCalendarEntries_ShiftId",
                schema: "config",
                table: "WorkCenterCalendarEntries",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterCalendarEntries_TenantId",
                schema: "config",
                table: "WorkCenterCalendarEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterCalendarEntries_WorkCenterCalendarId_DayOfWeek",
                schema: "config",
                table: "WorkCenterCalendarEntries",
                columns: new[] { "WorkCenterCalendarId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterCalendars_MachineId",
                schema: "config",
                table: "WorkCenterCalendars",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterCalendars_TenantId",
                schema: "config",
                table: "WorkCenterCalendars",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterCalendars_TenantId_MachineId",
                schema: "config",
                table: "WorkCenterCalendars",
                columns: new[] { "TenantId", "MachineId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkCenterCalendarEntries",
                schema: "config");

            migrationBuilder.DropTable(
                name: "Shifts",
                schema: "config");

            migrationBuilder.DropTable(
                name: "WorkCenterCalendars",
                schema: "config");
        }
    }
}
