using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_InventoryMovementId",
                table: "PurchaseReceipts",
                column: "InventoryMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_ProductId",
                table: "PurchaseReceipts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_SupplierId",
                table: "PurchaseReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_WarehouseId",
                table: "PurchaseReceipts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_WorkspaceId_IdempotencyKey",
                table: "PurchaseReceipts",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_WorkspaceId_ReceivedAtUtc_Id",
                table: "PurchaseReceipts",
                columns: new[] { "WorkspaceId", "ReceivedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseReceipts");
        }
    }
}
