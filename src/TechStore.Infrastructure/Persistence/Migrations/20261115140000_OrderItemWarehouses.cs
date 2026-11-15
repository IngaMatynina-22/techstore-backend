using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TechStore.Infrastructure.Persistence;

#nullable disable

namespace TechStore.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261115140000_OrderItemWarehouses")]
public partial class OrderItemWarehouses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WarehouseId",
            table: "OrderItems",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrderItems_WarehouseId",
            table: "OrderItems",
            column: "WarehouseId");

        migrationBuilder.AddForeignKey(
            name: "FK_OrderItems_Warehouses_WarehouseId",
            table: "OrderItems",
            column: "WarehouseId",
            principalTable: "Warehouses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            UPDATE [OrderItems]
            SET [WarehouseId] = [Orders].[WarehouseId]
            FROM [OrderItems]
            INNER JOIN [Orders] ON [OrderItems].[OrderId] = [Orders].[Id]
            WHERE [Orders].[WarehouseId] IS NOT NULL;
            """);

        migrationBuilder.AddColumn<int>(
            name: "WarehouseId",
            table: "ReturnItems",
            type: "int",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE [ReturnItems]
            SET [WarehouseId] = COALESCE([OrderItems].[WarehouseId], [ReturnRequests].[WarehouseId])
            FROM [ReturnItems]
            INNER JOIN [OrderItems] ON [ReturnItems].[OrderItemId] = [OrderItems].[Id]
            INNER JOIN [ReturnRequests] ON [ReturnItems].[ReturnRequestId] = [ReturnRequests].[Id];
            """);

        migrationBuilder.AlterColumn<int>(
            name: "WarehouseId",
            table: "ReturnItems",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReturnItems_WarehouseId",
            table: "ReturnItems",
            column: "WarehouseId");

        migrationBuilder.AddForeignKey(
            name: "FK_ReturnItems_Warehouses_WarehouseId",
            table: "ReturnItems",
            column: "WarehouseId",
            principalTable: "Warehouses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropForeignKey(
            name: "FK_ReturnRequests_Warehouses_WarehouseId",
            table: "ReturnRequests");

        migrationBuilder.AlterColumn<int>(
            name: "WarehouseId",
            table: "ReturnRequests",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");

        migrationBuilder.AddForeignKey(
            name: "FK_ReturnRequests_Warehouses_WarehouseId",
            table: "ReturnRequests",
            column: "WarehouseId",
            principalTable: "Warehouses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [ReturnRequests] WHERE [WarehouseId] IS NULL)
                THROW 51000, 'Cannot restore one warehouse per return request while any request covers multiple warehouses.', 1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_ReturnRequests_Warehouses_WarehouseId",
            table: "ReturnRequests");

        migrationBuilder.AlterColumn<int>(
            name: "WarehouseId",
            table: "ReturnRequests",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_ReturnRequests_Warehouses_WarehouseId",
            table: "ReturnRequests",
            column: "WarehouseId",
            principalTable: "Warehouses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropForeignKey(
            name: "FK_ReturnItems_Warehouses_WarehouseId",
            table: "ReturnItems");

        migrationBuilder.DropIndex(
            name: "IX_ReturnItems_WarehouseId",
            table: "ReturnItems");

        migrationBuilder.DropColumn(
            name: "WarehouseId",
            table: "ReturnItems");

        migrationBuilder.DropForeignKey(
            name: "FK_OrderItems_Warehouses_WarehouseId",
            table: "OrderItems");

        migrationBuilder.DropIndex(
            name: "IX_OrderItems_WarehouseId",
            table: "OrderItems");

        migrationBuilder.DropColumn(
            name: "WarehouseId",
            table: "OrderItems");
    }
}
