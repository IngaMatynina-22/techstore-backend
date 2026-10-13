using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryConcurrencyAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuantityAfter",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantityBefore",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferGroupId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "ProductVariants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Inventory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "StockOperationIdempotency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockOperationIdempotency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockOperationIdempotency_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_MovementType",
                table: "StockMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TransferGroupId",
                table: "StockMovements",
                column: "TransferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_LowStockThreshold",
                table: "ProductVariants",
                column: "LowStockThreshold");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariants_LowStockThreshold",
                table: "ProductVariants",
                sql: "[LowStockThreshold] IS NULL OR [LowStockThreshold] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_QuantityOnHand",
                table: "Inventory",
                column: "QuantityOnHand");

            migrationBuilder.CreateIndex(
                name: "IX_StockOperationIdempotency_IdempotencyKey",
                table: "StockOperationIdempotency",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockOperationIdempotency_UserId",
                table: "StockOperationIdempotency",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockOperationIdempotency");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_MovementType",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TransferGroupId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_LowStockThreshold",
                table: "ProductVariants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariants_LowStockThreshold",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_QuantityOnHand",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "QuantityAfter",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "QuantityBefore",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "TransferGroupId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Inventory");
        }
    }
}
