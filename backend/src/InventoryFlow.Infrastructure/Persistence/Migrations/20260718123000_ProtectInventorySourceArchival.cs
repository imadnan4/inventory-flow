using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProtectInventorySourceArchival : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_InventoryBalances_WorkspaceId_ProductId",
            table: "InventoryBalances",
            columns: new[] { "WorkspaceId", "ProductId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_InventoryBalances_WorkspaceId_ProductId",
            table: "InventoryBalances");
    }
}
