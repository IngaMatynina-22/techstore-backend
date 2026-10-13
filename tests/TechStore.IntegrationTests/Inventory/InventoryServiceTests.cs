using Microsoft.EntityFrameworkCore;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.Products;
using TechStore.Application.Common;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Services;
using TechStore.IntegrationTests.Catalog;

namespace TechStore.IntegrationTests.Inventory;

public class InventoryServiceTests
{
    private sealed class Fixture
    {
        public required ApplicationDbContext Db { get; init; }
        public required IInventoryService Inventory { get; init; }
        public required IWarehouseService Warehouses { get; init; }
        public required int WarehouseA { get; init; }
        public required int WarehouseB { get; init; }
        public required int VariantId { get; init; }
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var db = CatalogTestHelper.CreateContext();
        var categories = new CategoryService(db);
        var products = new ProductService(db);
        var warehouses = new WarehouseService(db);
        var inventory = new InventoryService(db);

        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "Inv Cat", Slug = "inv-cat-" + Guid.NewGuid().ToString("N")[..8] });
        var product = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Inv Product",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "INV-" + Guid.NewGuid().ToString("N")[..8],
                    VariantName = "Default",
                    PurchasePrice = 50,
                    SellingPrice = 80
                }
            ]
        }, null);

        var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;

        var a = await warehouses.CreateAsync(new CreateWarehouseRequest { Name = "A", Code = "WA-" + Guid.NewGuid().ToString("N")[..6] });
        var b = await warehouses.CreateAsync(new CreateWarehouseRequest { Name = "B", Code = "WB-" + Guid.NewGuid().ToString("N")[..6] });

        return new Fixture
        {
            Db = db,
            Inventory = inventory,
            Warehouses = warehouses,
            WarehouseA = a.Value!.Id,
            WarehouseB = b.Value!.Id,
            VariantId = variantId
        };
    }

    [Fact]
    public async Task ReceiptAndIssue_UpdateStockAndHistory()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        var receipt = await fx.Inventory.ReceiveAsync(new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 10,
            Comment = "Initial stock"
        }, userId: 1);

        Assert.True(receipt.IsSuccess);
        Assert.Equal(10, receipt.Value!.QuantityOnHand);

        var issue = await fx.Inventory.IssueAsync(new IssueRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 4
        }, userId: 1);

        Assert.True(issue.IsSuccess);
        Assert.Equal(6, issue.Value!.QuantityOnHand);

        var movements = await fx.Inventory.GetMovementsAsync(new StockMovementQuery
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            PageSize = 20
        });

        Assert.Equal(2, movements.TotalCount);
        Assert.Contains(movements.Items, m => m.MovementType == StockMovementType.Receipt && m.Quantity == 10);
        Assert.Contains(movements.Items, m => m.MovementType == StockMovementType.WriteOff && m.Quantity == -4);
    }

    [Fact]
    public async Task Issue_WhenInsufficient_FailsWithoutChangingStock()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        await fx.Inventory.ReceiveAsync(new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 3
        }, null);

        var issue = await fx.Inventory.IssueAsync(new IssueRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 5
        }, null);

        Assert.True(issue.IsFailure);
        Assert.Equal(ResultCodes.InsufficientStock, issue.Code);

        var item = await fx.Inventory.GetAsync(fx.WarehouseA, fx.VariantId);
        Assert.Equal(3, item!.QuantityOnHand);
    }

    [Fact]
    public async Task Adjustment_ComputesDeltaServerSide()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        await fx.Inventory.ReceiveAsync(new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 12
        }, null);

        var adjust = await fx.Inventory.AdjustAsync(new AdjustmentRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            TargetQuantityOnHand = 9,
            Reason = "Cycle count"
        }, null);

        Assert.True(adjust.IsSuccess);
        Assert.Equal(9, adjust.Value!.QuantityOnHand);
        Assert.Equal(-3, adjust.Value.QuantityAfter - adjust.Value.QuantityBefore);

        var zero = await fx.Inventory.AdjustAsync(new AdjustmentRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            TargetQuantityOnHand = 9,
            Reason = "No change"
        }, null);

        Assert.True(zero.IsFailure);
        Assert.Equal(ResultCodes.NoChange, zero.Code);
    }

    [Fact]
    public async Task Transfer_MovesStockAtomically()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        await fx.Inventory.ReceiveAsync(new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 8
        }, null);

        var transfer = await fx.Inventory.TransferAsync(new TransferRequest
        {
            SourceWarehouseId = fx.WarehouseA,
            DestinationWarehouseId = fx.WarehouseB,
            VariantId = fx.VariantId,
            Quantity = 5,
            Comment = "Rebalance"
        }, null);

        Assert.True(transfer.IsSuccess);
        Assert.NotEqual(Guid.Empty, transfer.Value!.TransferGroupId);

        var source = await fx.Inventory.GetAsync(fx.WarehouseA, fx.VariantId);
        var dest = await fx.Inventory.GetAsync(fx.WarehouseB, fx.VariantId);
        Assert.Equal(3, source!.QuantityOnHand);
        Assert.Equal(5, dest!.QuantityOnHand);

        var linked = await fx.Inventory.GetMovementsAsync(new StockMovementQuery
        {
            TransferGroupId = transfer.Value.TransferGroupId
        });
        Assert.Equal(2, linked.TotalCount);
    }

    [Fact]
    public async Task Transfer_SameWarehouse_Fails()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        var result = await fx.Inventory.TransferAsync(new TransferRequest
        {
            SourceWarehouseId = fx.WarehouseA,
            DestinationWarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 1
        }, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task IdempotentReceipt_DoesNotDoubleApply()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        var request = new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 4,
            IdempotencyKey = "receipt-key-1"
        };

        var first = await fx.Inventory.ReceiveAsync(request, null);
        var second = await fx.Inventory.ReceiveAsync(request, null);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(first.Value!.ReplayFromIdempotency);
        Assert.True(second.Value!.ReplayFromIdempotency);
        Assert.Equal(first.Value.MovementId, second.Value.MovementId);

        var item = await fx.Inventory.GetAsync(fx.WarehouseA, fx.VariantId);
        Assert.Equal(4, item!.QuantityOnHand);
    }

    [Fact]
    public async Task ConcurrentIssues_DoNotOversell_OnSqlServer()
    {
        // InMemory cannot prove SQL Server concurrency; this test uses LocalDB.
        var dbName = "TechStore_InvConcurrency_" + Guid.NewGuid().ToString("N");
        await using var db = CatalogTestHelper.CreateSqlServerContext(dbName);
        try
        {
            var categories = new CategoryService(db);
            var products = new ProductService(db);
            var warehouses = new WarehouseService(db);

            var category = await categories.CreateAsync(new CreateCategoryRequest
            {
                Name = "Conc",
                Slug = "conc-" + Guid.NewGuid().ToString("N")[..8]
            });
            Assert.True(category.IsSuccess);

            var product = await products.CreateAsync(new CreateProductRequest
            {
                Name = "Conc Product",
                CategoryId = category.Value!,
                Variants =
                [
                    new ProductVariantInput
                    {
                        Sku = "CONC-" + Guid.NewGuid().ToString("N")[..8],
                        VariantName = "Default",
                        PurchasePrice = 10,
                        SellingPrice = 20
                    }
                ]
            }, null);
            Assert.True(product.IsSuccess);

            var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;
            var warehouse = await warehouses.CreateAsync(new CreateWarehouseRequest
            {
                Name = "Conc WH",
                Code = "C" + Guid.NewGuid().ToString("N")[..5]
            });
            Assert.True(warehouse.IsSuccess);

            // Separate DbContext instances per concurrent operation (real connection pool behavior).
            async Task<Result<StockOperationResultDto>> IssueOnceAsync()
            {
                await using var scoped = CatalogTestHelper.CreateSqlServerContext(dbName, migrate: false);
                var scopedInventory = new InventoryService(scoped);
                return await scopedInventory.IssueAsync(new IssueRequest
                {
                    WarehouseId = warehouse.Value!.Id,
                    VariantId = variantId,
                    Quantity = 2
                }, null);
            }

            await using (var seedCtx = CatalogTestHelper.CreateSqlServerContext(dbName, migrate: false))
            {
                var seedInventory = new InventoryService(seedCtx);
                var receipt = await seedInventory.ReceiveAsync(new ReceiptRequest
                {
                    WarehouseId = warehouse.Value!.Id,
                    VariantId = variantId,
                    Quantity = 10
                }, null);
                Assert.True(receipt.IsSuccess);
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => IssueOnceAsync()));
            var successCount = results.Count(r => r.IsSuccess);
            Assert.True(successCount <= 5);
            Assert.Equal(8, successCount + results.Count(r => r.IsFailure));

            await using var verify = CatalogTestHelper.CreateSqlServerContext(dbName, migrate: false);
            var item = await new InventoryService(verify).GetAsync(warehouse.Value!.Id, variantId);
            Assert.NotNull(item);
            Assert.True(item!.QuantityOnHand >= 0);
            Assert.Equal(10 - successCount * 2, item.QuantityOnHand);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task LowStockAndSummary_Work()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        await fx.Inventory.SetLowStockThresholdAsync(fx.VariantId, new SetLowStockThresholdRequest { LowStockThreshold = 5 });
        await fx.Inventory.ReceiveAsync(new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 3
        }, null);

        var low = await fx.Inventory.GetLowStockAsync(new InventoryQuery { WarehouseId = fx.WarehouseA });
        Assert.Contains(low.Items, i => i.VariantId == fx.VariantId && i.IsBelowThreshold);

        var summary = await fx.Inventory.GetWarehouseSummaryAsync(fx.WarehouseA);
        Assert.NotNull(summary);
        Assert.Equal(3, summary!.TotalUnitsOnHand);
        Assert.True(summary.LowStockSkuCount >= 1);

        var valuation = await fx.Inventory.GetInventoryValuationAsync(fx.WarehouseA);
        Assert.NotNull(valuation);
        Assert.Equal(150m, valuation!.ValuedAmount);
    }

    [Fact]
    public async Task InventoryPaging_FiltersOnServer()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        await fx.Inventory.ReceiveAsync(new ReceiptRequest
        {
            WarehouseId = fx.WarehouseA,
            VariantId = fx.VariantId,
            Quantity = 2
        }, null);

        var page = await fx.Inventory.GetPagedAsync(new InventoryQuery
        {
            WarehouseId = fx.WarehouseA,
            Page = 1,
            PageSize = 10,
            SortBy = "quantity",
            SortDesc = true
        });

        Assert.True(page.TotalCount >= 1);
        Assert.Contains(page.Items, i => i.VariantId == fx.VariantId);
    }
}
