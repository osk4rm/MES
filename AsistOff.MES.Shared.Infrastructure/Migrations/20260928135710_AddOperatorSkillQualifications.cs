using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorSkillQualifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorSkillQualifications",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSkillQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorSkillQualifications_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "config",
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorSkillQualifications_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "config",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSkillQualifications_OperatorId",
                schema: "config",
                table: "OperatorSkillQualifications",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSkillQualifications_SkillId",
                schema: "config",
                table: "OperatorSkillQualifications",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSkillQualifications_TenantId",
                schema: "config",
                table: "OperatorSkillQualifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSkillQualifications_TenantId_OperatorId_SkillId",
                schema: "config",
                table: "OperatorSkillQualifications",
                columns: new[] { "TenantId", "OperatorId", "SkillId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorSkillQualifications",
                schema: "config");
        }
    }
}
