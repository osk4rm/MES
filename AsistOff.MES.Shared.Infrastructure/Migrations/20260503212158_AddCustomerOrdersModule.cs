using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerOrdersModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customer_orders");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "customer_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrders",
                schema: "customer_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCodeSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerNameSnapshot = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CustomerTaxIdSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomerAddressSnapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TotalNetAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TotalGrossAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customer_orders",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrderLines",
                schema: "customer_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalLineId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCodeSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    MeasureUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    MeasureUnitCodeSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReleasedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UnitNetPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LineNetAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrderLines_CustomerOrders_CustomerOrderId",
                        column: x => x.CustomerOrderId,
                        principalSchema: "customer_orders",
                        principalTable: "CustomerOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrderLineProductionReleases",
                schema: "customer_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PlannedStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlannedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrderLineProductionReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrderLineProductionReleases_CustomerOrderLines_Cust~",
                        column: x => x.CustomerOrderLineId,
                        principalSchema: "customer_orders",
                        principalTable: "CustomerOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLineProductionReleases_CustomerOrderLineId",
                schema: "customer_orders",
                table: "CustomerOrderLineProductionReleases",
                column: "CustomerOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLineProductionReleases_ProductionOrderId",
                schema: "customer_orders",
                table: "CustomerOrderLineProductionReleases",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLineProductionReleases_TenantId_CustomerOrderL~",
                schema: "customer_orders",
                table: "CustomerOrderLineProductionReleases",
                columns: new[] { "TenantId", "CustomerOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLineProductionReleases_TenantId_RecipeId",
                schema: "customer_orders",
                table: "CustomerOrderLineProductionReleases",
                columns: new[] { "TenantId", "RecipeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLines_CustomerOrderId",
                schema: "customer_orders",
                table: "CustomerOrderLines",
                column: "CustomerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLines_TenantId_CustomerOrderId",
                schema: "customer_orders",
                table: "CustomerOrderLines",
                columns: new[] { "TenantId", "CustomerOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLines_TenantId_ProductId",
                schema: "customer_orders",
                table: "CustomerOrderLines",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderLines_TenantId_Status",
                schema: "customer_orders",
                table: "CustomerOrderLines",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_CustomerId",
                schema: "customer_orders",
                table: "CustomerOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_RequestedDeliveryDate",
                schema: "customer_orders",
                table: "CustomerOrders",
                column: "RequestedDeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_TenantId_CustomerId",
                schema: "customer_orders",
                table: "CustomerOrders",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_TenantId_ExternalSystem_ExternalOrderId",
                schema: "customer_orders",
                table: "CustomerOrders",
                columns: new[] { "TenantId", "ExternalSystem", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_TenantId_OrderNumber",
                schema: "customer_orders",
                table: "CustomerOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_TenantId_Status",
                schema: "customer_orders",
                table: "CustomerOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SyncId",
                schema: "customer_orders",
                table: "Customers",
                column: "SyncId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                schema: "customer_orders",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Code",
                schema: "customer_orders",
                table: "Customers",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerOrderLineProductionReleases",
                schema: "customer_orders");

            migrationBuilder.DropTable(
                name: "CustomerOrderLines",
                schema: "customer_orders");

            migrationBuilder.DropTable(
                name: "CustomerOrders",
                schema: "customer_orders");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "customer_orders");
        }
    }
}
