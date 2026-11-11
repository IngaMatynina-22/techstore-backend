using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TechStore.Infrastructure.Persistence;

#nullable disable

namespace TechStore.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261110100000_ManagerWarehouseAndUkCategories")]
public partial class ManagerWarehouseAndUkCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Orders_Warehouses_WarehouseId",
            table: "Orders");

        migrationBuilder.AlterColumn<int>(
            name: "WarehouseId",
            table: "Orders",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");

        migrationBuilder.AddForeignKey(
            name: "FK_Orders_Warehouses_WarehouseId",
            table: "Orders",
            column: "WarehouseId",
            principalTable: "Warehouses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            UPDATE [Categories] SET [Name] = N'Електроніка', [Description] = N'Уся електроніка' WHERE [Slug] = N'electronics';
            UPDATE [Categories] SET [Name] = N'Ноутбуки', [Description] = N'Портативні комп’ютери' WHERE [Slug] = N'laptops';
            UPDATE [Categories] SET [Name] = N'Смартфони', [Description] = N'Мобільні телефони' WHERE [Slug] = N'phones';
            UPDATE [Categories] SET [Name] = N'Аксесуари', [Description] = N'Клавіатури, миші, чохли' WHERE [Slug] = N'accessories';
            UPDATE [Categories] SET [Name] = N'Комплектуючі', [Description] = N'Комплектуючі для комп’ютерів' WHERE [Slug] = N'components';
            UPDATE [Categories] SET [Name] = N'Накопичувачі', [Description] = N'SSD / HDD' WHERE [Slug] = N'storage';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Categories] SET [Name] = N'Electronics', [Description] = N'All electronics' WHERE [Slug] = N'electronics';
            UPDATE [Categories] SET [Name] = N'Laptops', [Description] = N'Notebooks' WHERE [Slug] = N'laptops';
            UPDATE [Categories] SET [Name] = N'Phones', [Description] = N'Smartphones' WHERE [Slug] = N'phones';
            UPDATE [Categories] SET [Name] = N'Accessories', [Description] = N'Keyboards, mice, cases' WHERE [Slug] = N'accessories';
            UPDATE [Categories] SET [Name] = N'Components', [Description] = N'PC parts' WHERE [Slug] = N'components';
            UPDATE [Categories] SET [Name] = N'Storage', [Description] = N'SSD / HDD' WHERE [Slug] = N'storage';
            """);

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [Orders] WHERE [WarehouseId] IS NULL)
               AND NOT EXISTS (SELECT 1 FROM [Warehouses])
                THROW 51000, 'Cannot restore required order warehouses because no warehouses exist.', 1;
            UPDATE [Orders]
            SET [WarehouseId] = (SELECT TOP (1) [Id] FROM [Warehouses] ORDER BY [Id])
            WHERE [WarehouseId] IS NULL;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_Orders_Warehouses_WarehouseId",
            table: "Orders");

        migrationBuilder.AlterColumn<int>(
            name: "WarehouseId",
            table: "Orders",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Orders_Warehouses_WarehouseId",
            table: "Orders",
            column: "WarehouseId",
            principalTable: "Warehouses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
