using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpcMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpcMeasurements",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacteristicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpcMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpcMeasurements_SpcCharacteristics_CharacteristicId",
                        column: x => x.CharacteristicId,
                        principalSchema: "production",
                        principalTable: "SpcCharacteristics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpcMeasurements_CharacteristicId",
                schema: "production",
                table: "SpcMeasurements",
                column: "CharacteristicId");

            migrationBuilder.CreateIndex(
                name: "IX_SpcMeasurements_TenantId",
                schema: "production",
                table: "SpcMeasurements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SpcMeasurements_TenantId_CharacteristicId_MeasuredAt",
                schema: "production",
                table: "SpcMeasurements",
                columns: new[] { "TenantId", "CharacteristicId", "MeasuredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpcMeasurements",
                schema: "production");
        }
    }
}
