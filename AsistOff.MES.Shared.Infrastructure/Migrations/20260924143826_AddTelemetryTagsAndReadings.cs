using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryTagsAndReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineTelemetryTags",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<short>(type: "smallint", nullable: false),
                    PollIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineTelemetryTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryReadings",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoubleValue = table.Column<double>(type: "double precision", nullable: true),
                    StringValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Quality = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryReadings_MachineTelemetryTags_TagId",
                        column: x => x.TagId,
                        principalSchema: "production",
                        principalTable: "MachineTelemetryTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineTelemetryTags_TenantId_MachineId_IsEnabled",
                schema: "production",
                table: "MachineTelemetryTags",
                columns: new[] { "TenantId", "MachineId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineTelemetryTags_TenantId_MachineId_NodeId",
                schema: "production",
                table: "MachineTelemetryTags",
                columns: new[] { "TenantId", "MachineId", "NodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryReadings_TagId",
                schema: "production",
                table: "TelemetryReadings",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryReadings_TenantId_MachineId_ReadAt",
                schema: "production",
                table: "TelemetryReadings",
                columns: new[] { "TenantId", "MachineId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryReadings_TenantId_TagId_ReadAt",
                schema: "production",
                table: "TelemetryReadings",
                columns: new[] { "TenantId", "TagId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemetryReadings",
                schema: "production");

            migrationBuilder.DropTable(
                name: "MachineTelemetryTags",
                schema: "production");
        }
    }
}
