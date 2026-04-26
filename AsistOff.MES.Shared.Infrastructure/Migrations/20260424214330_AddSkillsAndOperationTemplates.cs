using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillsAndOperationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationTemplates",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SetupTimeMinutes = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    RunTimeMode = table.Column<short>(type: "smallint", nullable: false),
                    RunTimePerUnitSeconds = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    RunTimePerBatchMinutes = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    TeardownTimeMinutes = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    QueueTimeMinutes = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationTemplates_TenantId",
                schema: "production",
                table: "OperationTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationTemplates_TenantId_Code",
                schema: "production",
                table: "OperationTemplates",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TenantId",
                schema: "config",
                table: "Skills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TenantId_Code",
                schema: "config",
                table: "Skills",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationTemplates",
                schema: "production");

            migrationBuilder.DropTable(
                name: "Skills",
                schema: "config");
        }
    }
}
