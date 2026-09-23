using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReasonCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReasonCodes",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReasonCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReasonCodes_Category",
                schema: "config",
                table: "ReasonCodes",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ReasonCodes_TenantId",
                schema: "config",
                table: "ReasonCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReasonCodes_TenantId_Code",
                schema: "config",
                table: "ReasonCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReasonCodes",
                schema: "config");
        }
    }
}
