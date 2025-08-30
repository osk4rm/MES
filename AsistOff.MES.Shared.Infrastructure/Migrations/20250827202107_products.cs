using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class products : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeasureUnits",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    BaseUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasureUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeasureUnits_MeasureUnits_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalSchema: "config",
                        principalTable: "MeasureUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductGroups",
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
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductGroups_ProductGroups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalSchema: "config",
                        principalTable: "ProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Ean = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ScanBy = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_ProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalSchema: "config",
                        principalTable: "ProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductMeasureUnits",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "text", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasureUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false, defaultValue: 1m),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMeasureUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductMeasureUnits_MeasureUnits_MeasureUnitId",
                        column: x => x.MeasureUnitId,
                        principalSchema: "config",
                        principalTable: "MeasureUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductMeasureUnits_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "config",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "text", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "PLN"),
                    MeasureUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPrices_MeasureUnits_MeasureUnitId",
                        column: x => x.MeasureUnitId,
                        principalSchema: "config",
                        principalTable: "MeasureUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductPrices_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "config",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeasureUnits_BaseUnitId",
                schema: "config",
                table: "MeasureUnits",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasureUnits_TenantId",
                schema: "config",
                table: "MeasureUnits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasureUnits_TenantId_Symbol_Type",
                schema: "config",
                table: "MeasureUnits",
                columns: new[] { "TenantId", "Symbol", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasureUnits_Type",
                schema: "config",
                table: "MeasureUnits",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroups_ParentGroupId",
                schema: "config",
                table: "ProductGroups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroups_TenantId",
                schema: "config",
                table: "ProductGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroups_TenantId_Code",
                schema: "config",
                table: "ProductGroups",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductMeasureUnits_IsDefault",
                schema: "config",
                table: "ProductMeasureUnits",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMeasureUnits_MeasureUnitId",
                schema: "config",
                table: "ProductMeasureUnits",
                column: "MeasureUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMeasureUnits_ProductId",
                schema: "config",
                table: "ProductMeasureUnits",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMeasureUnits_TenantId",
                schema: "config",
                table: "ProductMeasureUnits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMeasureUnits_TenantId_ProductId_MeasureUnitId",
                schema: "config",
                table: "ProductMeasureUnits",
                columns: new[] { "TenantId", "ProductId", "MeasureUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_MeasureUnitId",
                schema: "config",
                table: "ProductPrices",
                column: "MeasureUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_PriceType",
                schema: "config",
                table: "ProductPrices",
                column: "PriceType");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ProductId",
                schema: "config",
                table: "ProductPrices",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ProductId_PriceType_ValidFrom_ValidTo",
                schema: "config",
                table: "ProductPrices",
                columns: new[] { "ProductId", "PriceType", "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_TenantId",
                schema: "config",
                table: "ProductPrices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ValidFrom",
                schema: "config",
                table: "ProductPrices",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ValidTo",
                schema: "config",
                table: "ProductPrices",
                column: "ValidTo");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: "config",
                table: "Products",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Ean",
                schema: "config",
                table: "Products",
                column: "Ean");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductGroupId",
                schema: "config",
                table: "Products",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ScanBy",
                schema: "config",
                table: "Products",
                column: "ScanBy");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId",
                schema: "config",
                table: "Products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Code",
                schema: "config",
                table: "Products",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductMeasureUnits",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ProductPrices",
                schema: "config");

            migrationBuilder.DropTable(
                name: "MeasureUnits",
                schema: "config");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ProductGroups",
                schema: "config");
        }
    }
}
