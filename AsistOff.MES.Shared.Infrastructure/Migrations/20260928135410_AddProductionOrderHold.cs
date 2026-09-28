using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrderHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HeldAtUtc",
                schema: "production",
                table: "ProductionOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                schema: "production",
                table: "ProductionOrders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "StatusBeforeHold",
                schema: "production",
                table: "ProductionOrders",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeldAtUtc",
                schema: "production",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                schema: "production",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "StatusBeforeHold",
                schema: "production",
                table: "ProductionOrders");
        }
    }
}
