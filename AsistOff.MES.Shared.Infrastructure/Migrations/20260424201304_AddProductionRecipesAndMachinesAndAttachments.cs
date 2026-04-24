using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionRecipesAndMachinesAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.EnsureSchema(
                name: "production");

            migrationBuilder.CreateTable(
                name: "Attachments",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Machines_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "config",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PrimaryProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangeNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeVersions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "production",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationNodes",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SortIndex = table.Column<int>(type: "integer", nullable: false),
                    SetupTimeMinutes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    RunTimeMode = table.Column<short>(type: "smallint", nullable: false),
                    RunTimePerUnitSeconds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    RunTimePerBatchMinutes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TeardownTimeMinutes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    QueueTimeMinutes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    AllowParallelExecution = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationNodes_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalSchema: "production",
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BomItems",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasureUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityType = table.Column<short>(type: "smallint", nullable: false),
                    ScrapPercentage = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    PreferredWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumptionTiming = table.Column<short>(type: "smallint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BomItems_OperationNodes_OperationNodeId",
                        column: x => x.OperationNodeId,
                        principalSchema: "production",
                        principalTable: "OperationNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationDependencies",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredecessorOperationNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependencyType = table.Column<short>(type: "smallint", nullable: false),
                    LagMinutes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationDependencies_OperationNodes_OperationNodeId",
                        column: x => x.OperationNodeId,
                        principalSchema: "production",
                        principalTable: "OperationNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationDependencies_OperationNodes_PredecessorOperationNo~",
                        column: x => x.PredecessorOperationNodeId,
                        principalSchema: "production",
                        principalTable: "OperationNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationOutputs",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasureUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityType = table.Column<short>(type: "smallint", nullable: false),
                    OutputType = table.Column<short>(type: "smallint", nullable: false),
                    PreferredWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationOutputs_OperationNodes_OperationNodeId",
                        column: x => x.OperationNodeId,
                        principalSchema: "production",
                        principalTable: "OperationNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceRequirements",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredMachineId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredCapability = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    RequiredOperatorCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceRequirements_OperationNodes_OperationNodeId",
                        column: x => x.OperationNodeId,
                        principalSchema: "production",
                        principalTable: "OperationNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerType_OwnerId",
                schema: "files",
                table: "Attachments",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId",
                schema: "files",
                table: "Attachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_OperationNodeId",
                schema: "production",
                table: "BomItems",
                column: "OperationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_ProductId",
                schema: "production",
                table: "BomItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_TenantId",
                schema: "production",
                table: "BomItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_DepartmentId",
                schema: "config",
                table: "Machines",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_TenantId",
                schema: "config",
                table: "Machines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_TenantId_Code",
                schema: "config",
                table: "Machines",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationDependencies_OperationNodeId_PredecessorOperationN~",
                schema: "production",
                table: "OperationDependencies",
                columns: new[] { "OperationNodeId", "PredecessorOperationNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationDependencies_PredecessorOperationNodeId",
                schema: "production",
                table: "OperationDependencies",
                column: "PredecessorOperationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationDependencies_RecipeVersionId",
                schema: "production",
                table: "OperationDependencies",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationDependencies_TenantId",
                schema: "production",
                table: "OperationDependencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationNodes_RecipeVersionId_Code",
                schema: "production",
                table: "OperationNodes",
                columns: new[] { "RecipeVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationNodes_TenantId",
                schema: "production",
                table: "OperationNodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationOutputs_OperationNodeId",
                schema: "production",
                table: "OperationOutputs",
                column: "OperationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationOutputs_ProductId",
                schema: "production",
                table: "OperationOutputs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationOutputs_TenantId",
                schema: "production",
                table: "OperationOutputs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CurrentVersionId",
                schema: "production",
                table: "Recipes",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TenantId",
                schema: "production",
                table: "Recipes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TenantId_Code",
                schema: "production",
                table: "Recipes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_RecipeId_Status",
                schema: "production",
                table: "RecipeVersions",
                columns: new[] { "RecipeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_RecipeId_VersionNumber",
                schema: "production",
                table: "RecipeVersions",
                columns: new[] { "RecipeId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_TenantId",
                schema: "production",
                table: "RecipeVersions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequirements_OperationNodeId",
                schema: "production",
                table: "ResourceRequirements",
                column: "OperationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequirements_TenantId",
                schema: "production",
                table: "ResourceRequirements",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "files");

            migrationBuilder.DropTable(
                name: "BomItems",
                schema: "production");

            migrationBuilder.DropTable(
                name: "Machines",
                schema: "config");

            migrationBuilder.DropTable(
                name: "OperationDependencies",
                schema: "production");

            migrationBuilder.DropTable(
                name: "OperationOutputs",
                schema: "production");

            migrationBuilder.DropTable(
                name: "ResourceRequirements",
                schema: "production");

            migrationBuilder.DropTable(
                name: "OperationNodes",
                schema: "production");

            migrationBuilder.DropTable(
                name: "RecipeVersions",
                schema: "production");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "production");
        }
    }
}
