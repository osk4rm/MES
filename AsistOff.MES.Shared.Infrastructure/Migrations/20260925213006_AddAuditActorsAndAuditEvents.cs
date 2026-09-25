using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorsAndAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "UserRoles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "UserRoles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "config",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "config",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "SpcMeasurements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "SpcMeasurements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "SpcCharacteristics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "SpcCharacteristics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "ScrapEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "ScrapEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "Roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "Roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "RolePermissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "RolePermissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "RecipeVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "RecipeVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "ProductionOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "ProductionOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "ProductionConfirmations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "ProductionConfirmations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "Permissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "Permissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "OperationTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "OperationTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "OpcUaConnections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "OpcUaConnections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "MachineTelemetryTags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "MachineTelemetryTags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "config",
                table: "Machines",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "config",
                table: "Machines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "config",
                table: "Machines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "config",
                table: "Machines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "Lots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "Lots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "LotGenealogyEdges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "LotGenealogyEdges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "KanbanLoops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "KanbanLoops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "KanbanCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "KanbanCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "DowntimeEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "DowntimeEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "files",
                table: "Attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "files",
                table: "Attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "production",
                table: "AndonSignals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "production",
                table: "AndonSignals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<short>(type: "smallint", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId",
                schema: "shared",
                table: "AuditEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_EntityName_EntityId_ChangedAt",
                schema: "shared",
                table: "AuditEvents",
                columns: new[] { "TenantId", "EntityName", "EntityId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "shared");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "users",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "users",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "users",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "users",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "config",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "config",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "SpcMeasurements");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "SpcMeasurements");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "SpcCharacteristics");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "SpcCharacteristics");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "ScrapEvents");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "ScrapEvents");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "users",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "users",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "users",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "users",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "users",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "users",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "ProductionConfirmations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "ProductionConfirmations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "users",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "users",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "OperationTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "OperationTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "OpcUaConnections");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "OpcUaConnections");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "MachineTelemetryTags");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "MachineTelemetryTags");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "config",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "config",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "config",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "config",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "LotGenealogyEdges");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "LotGenealogyEdges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "KanbanLoops");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "KanbanLoops");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "DowntimeEvents");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "DowntimeEvents");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "files",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "files",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "production",
                table: "AndonSignals");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "production",
                table: "AndonSignals");
        }
    }
}
