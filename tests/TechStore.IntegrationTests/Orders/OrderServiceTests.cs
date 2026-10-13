using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.Orders;
using TechStore.Application.Features.Products;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Identity;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Persistence.Seed;
using TechStore.Infrastructure.Services;
using TechStore.IntegrationTests.Catalog;

namespace TechStore.IntegrationTests.Orders;

public class OrderServiceTests
{
    private sealed class Fixture
    {
        public required ApplicationDbContext Db { get; init; }
        public required IOrderService Orders { get; init; }
        public required IInventoryService Inventory { get; init; }
        public required int WarehouseId { get; init; }
        public required int VariantId { get; init; }
        public required int CustomerId { get; init; }
        public required ActorContext Staff { get; init; }
        public required ActorContext CustomerActor { get; init; }
    }

    private static async Task<Fixture> CreateFixtureAsync(int initialStock = 10)
    {
        var db = CatalogTestHelper.CreateContext();
        await DbSeeder.SeedOrderStatusesAsync(db);

        var categories = new CategoryService(db);
        var products = new ProductService(db);
        var warehouses = new WarehouseService(db);
        var inventory = new InventoryService(db);
        var orders = new OrderService(db, inventory);

        var category = await categories.CreateAsync(new CreateCategoryRequest
        {
            Name = "Ord Cat",
            Slug = "ord-cat-" + Guid.NewGuid().ToString("N")[..8]
        });

        var product = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Ord Product",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "ORD-" + Guid.NewGuid().ToString("N")[..8],
                    VariantName = "Default",
                    PurchasePrice = 40,
                    SellingPrice = 100
                }
            ]
        }, null);

        var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;
        var warehouse = await warehouses.CreateAsync(new CreateWarehouseRequest
        {
            Name = "Main",
            Code = "M" + Guid.NewGuid().ToString("N")[..5]
        });

        if (initialStock > 0)
        {
            await inventory.ReceiveAsync(new ReceiptRequest
            {
                WarehouseId = warehouse.Value!.Id,
                VariantId = variantId,
                Quantity = initialStock
            }, null);
        }

        var customer = new Customer
        {
            FirstName = "Ann",
            LastName = "Buyer",
            Email = "ann-" + Guid.NewGuid().ToString("N")[..6] + "@test.local",
            UserId = 9001,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return new Fixture
        {
            Db = db,
            Orders = orders,
            Inventory = inventory,
            WarehouseId = warehouse.Value!.Id,
            VariantId = variantId,
            CustomerId = customer.Id,
            Staff = new ActorContext { UserId = 1, IsStaff = true, IsCustomer = false },
            CustomerActor = new ActorContext { UserId = 9001, IsStaff = false, IsCustomer = true }
        };
    }

    private static CreateOrderRequest BuildRequest(Fixture fx, int qty, string? key = null) => new()
    {
        WarehouseId = fx.WarehouseId,
        CustomerId = fx.CustomerId,
        CustomerComment = "note",
        IdempotencyKey = key,
        Items = [new CreateOrderItemRequest { ProductVariantId = fx.VariantId, Quantity = qty }]
    };

    [Fact]
    public async Task Create_ReservesStock_AndSnapshotsPrice()
    {
        var fx = await CreateFixtureAsync(10);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(BuildRequest(fx, 3), fx.Staff);
        Assert.True(created.IsSuccess);
        Assert.Equal(300m, created.Value!.TotalAmount);
        Assert.Equal(100m, created.Value.Items[0].UnitPrice);
        Assert.Equal(OrderStatusCodes.New, created.Value.StatusCode);
        Assert.True(created.Value.HasActiveReservation);

        var stock = await fx.Inventory.GetAsync(fx.WarehouseId, fx.VariantId);
        Assert.Equal(10, stock!.QuantityOnHand);
        Assert.Equal(3, stock.QuantityReserved);
        Assert.Equal(7, stock.AvailableQuantity);
    }

    [Fact]
    public async Task Create_RejectsEmptyAndNonPositiveQty()
    {
        var fx = await CreateFixtureAsync();
        await using var _ = fx.Db;

        var empty = await fx.Orders.CreateAsync(new CreateOrderRequest
        {
            WarehouseId = fx.WarehouseId,
            CustomerId = fx.CustomerId,
            Items = []
        }, fx.Staff);
        Assert.True(empty.IsFailure);

        var zero = await fx.Orders.CreateAsync(BuildRequest(fx, 0), fx.Staff);
        Assert.True(zero.IsFailure);
    }

    [Fact]
    public async Task Create_RollsBackAllReserves_WhenOneLineInsufficient()
    {
        var fx = await CreateFixtureAsync(5);
        await using var _ = fx.Db;

        // Second variant with zero stock
        var products = new ProductService(fx.Db);
        var categories = new CategoryService(fx.Db);
        var cat = await categories.CreateAsync(new CreateCategoryRequest
        {
            Name = "C2",
            Slug = "c2-" + Guid.NewGuid().ToString("N")[..6]
        });
        var p2 = await products.CreateAsync(new CreateProductRequest
        {
            Name = "P2",
            CategoryId = cat.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "ORB-" + Guid.NewGuid().ToString("N")[..6],
                    VariantName = "B",
                    PurchasePrice = 10,
                    SellingPrice = 20
                }
            ]
        }, null);
        var variantB = (await products.GetByIdAsync(p2.Value!))!.Variants[0].Id;

        var result = await fx.Orders.CreateAsync(new CreateOrderRequest
        {
            WarehouseId = fx.WarehouseId,
            CustomerId = fx.CustomerId,
            Items =
            [
                new CreateOrderItemRequest { ProductVariantId = fx.VariantId, Quantity = 2 },
                new CreateOrderItemRequest { ProductVariantId = variantB, Quantity = 1 }
            ]
        }, fx.Staff);

        Assert.True(result.IsFailure);
        Assert.Equal(ResultCodes.InsufficientStock, result.Code);
        Assert.Equal(0, await fx.Db.Orders.CountAsync());

        var stock = await fx.Inventory.GetAsync(fx.WarehouseId, fx.VariantId);
        Assert.Equal(0, stock!.QuantityReserved);
    }

    [Fact]
    public async Task Idempotency_ReplaysSameCreate()
    {
        var fx = await CreateFixtureAsync(10);
        await using var _ = fx.Db;

        var key = "ord-key-" + Guid.NewGuid().ToString("N");
        var first = await fx.Orders.CreateAsync(BuildRequest(fx, 2, key), fx.Staff);
        var second = await fx.Orders.CreateAsync(BuildRequest(fx, 2, key), fx.Staff);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(first.Value!.ReplayFromIdempotency);
        Assert.True(second.Value!.ReplayFromIdempotency);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(1, await fx.Db.Orders.CountAsync());

        var stock = await fx.Inventory.GetAsync(fx.WarehouseId, fx.VariantId);
        Assert.Equal(2, stock!.QuantityReserved);
    }

    [Fact]
    public async Task Idempotency_ConflictOnDifferentPayload()
    {
        var fx = await CreateFixtureAsync(10);
        await using var _ = fx.Db;

        var key = "ord-conflict-" + Guid.NewGuid().ToString("N");
        Assert.True((await fx.Orders.CreateAsync(BuildRequest(fx, 2, key), fx.Staff)).IsSuccess);
        var conflict = await fx.Orders.CreateAsync(BuildRequest(fx, 3, key), fx.Staff);
        Assert.True(conflict.IsFailure);
        Assert.Equal(ResultCodes.IdempotencyConflict, conflict.Code);
    }

    [Fact]
    public async Task Lifecycle_ConfirmProcessComplete_FulfillsStock()
    {
        var fx = await CreateFixtureAsync(10);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(BuildRequest(fx, 4), fx.Staff);
        var id = created.Value!.Id;

        Assert.True((await fx.Orders.ConfirmAsync(id, fx.Staff)).IsSuccess);
        Assert.True((await fx.Orders.StartProcessingAsync(id, fx.Staff)).IsSuccess);
        var completed = await fx.Orders.CompleteAsync(id, fx.Staff);
        Assert.True(completed.IsSuccess);
        Assert.Equal(OrderStatusCodes.Completed, completed.Value!.StatusCode);
        Assert.False(completed.Value.HasActiveReservation);

        var stock = await fx.Inventory.GetAsync(fx.WarehouseId, fx.VariantId);
        Assert.Equal(6, stock!.QuantityOnHand);
        Assert.Equal(0, stock.QuantityReserved);

        var movements = await fx.Db.StockMovements.Where(m => m.ReferenceType == "Order" && m.ReferenceId == id).ToListAsync();
        Assert.Contains(movements, m => m.MovementType == StockMovementType.Reservation);
        Assert.Contains(movements, m => m.MovementType == StockMovementType.Sale);

        var history = await fx.Orders.GetHistoryAsync(id, fx.Staff);
        Assert.True(history.Count >= 4);
    }

    [Fact]
    public async Task Cancel_ReleasesReservation_Once()
    {
        var fx = await CreateFixtureAsync(10);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(BuildRequest(fx, 5), fx.Staff);
        var id = created.Value!.Id;

        Assert.True((await fx.Orders.CancelAsync(id, fx.Staff)).IsSuccess);
        var again = await fx.Orders.CancelAsync(id, fx.Staff);
        Assert.True(again.IsSuccess);
        Assert.Equal(OrderStatusCodes.Cancelled, again.Value!.StatusCode);

        var stock = await fx.Inventory.GetAsync(fx.WarehouseId, fx.VariantId);
        Assert.Equal(10, stock!.QuantityOnHand);
        Assert.Equal(0, stock.QuantityReserved);
    }

    [Fact]
    public async Task Customer_CannotReadForeignOrder()
    {
        var fx = await CreateFixtureAsync(5);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(BuildRequest(fx, 1), fx.Staff);
        var stranger = new ActorContext { UserId = 7777, IsStaff = false, IsCustomer = true };
        var details = await fx.Orders.GetByIdAsync(created.Value!.Id, stranger);
        Assert.Null(details);
    }

    [Fact]
    public async Task Customer_CanCreateOwnOrder()
    {
        var fx = await CreateFixtureAsync(5);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(new CreateOrderRequest
        {
            WarehouseId = fx.WarehouseId,
            // CustomerId from client must be ignored for customer role
            CustomerId = 999999,
            Items = [new CreateOrderItemRequest { ProductVariantId = fx.VariantId, Quantity = 1 }]
        }, fx.CustomerActor);

        Assert.True(created.IsSuccess);
        Assert.Equal(fx.CustomerId, created.Value!.CustomerId);
    }

    [Fact]
    public async Task InvalidTransition_IsRejected()
    {
        var fx = await CreateFixtureAsync(5);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(BuildRequest(fx, 1), fx.Staff);
        var complete = await fx.Orders.CompleteAsync(created.Value!.Id, fx.Staff);
        Assert.True(complete.IsFailure);
        Assert.Equal(ResultCodes.InvalidTransition, complete.Code);
    }

    [Fact]
    public async Task AggregatesDuplicateVariantLines()
    {
        var fx = await CreateFixtureAsync(10);
        await using var _ = fx.Db;

        var created = await fx.Orders.CreateAsync(new CreateOrderRequest
        {
            WarehouseId = fx.WarehouseId,
            CustomerId = fx.CustomerId,
            Items =
            [
                new CreateOrderItemRequest { ProductVariantId = fx.VariantId, Quantity = 2 },
                new CreateOrderItemRequest { ProductVariantId = fx.VariantId, Quantity = 3 }
            ]
        }, fx.Staff);

        Assert.True(created.IsSuccess);
        Assert.Single(created.Value!.Items);
        Assert.Equal(5, created.Value.Items[0].Quantity);
        Assert.Equal(500m, created.Value.TotalAmount);
    }

    [Fact]
    public async Task ConcurrentReservations_DoNotOversell_OnSqlServer()
    {
        var dbName = "TechStore_OrdConcurrency_" + Guid.NewGuid().ToString("N");
        await using var db = CatalogTestHelper.CreateSqlServerContext(dbName);
        try
        {
            await DbSeeder.SeedOrderStatusesAsync(db);
            var categories = new CategoryService(db);
            var products = new ProductService(db);
            var warehouses = new WarehouseService(db);

            var category = await categories.CreateAsync(new CreateCategoryRequest
            {
                Name = "ConcOrd",
                Slug = "conc-ord-" + Guid.NewGuid().ToString("N")[..8]
            });
            var product = await products.CreateAsync(new CreateProductRequest
            {
                Name = "ConcOrd Product",
                CategoryId = category.Value!,
                Variants =
                [
                    new ProductVariantInput
                    {
                        Sku = "ORC-" + Guid.NewGuid().ToString("N")[..8],
                        VariantName = "Default",
                        PurchasePrice = 10,
                        SellingPrice = 20
                    }
                ]
            }, null);
            var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;
            var warehouse = await warehouses.CreateAsync(new CreateWarehouseRequest
            {
                Name = "Conc WH",
                Code = "O" + Guid.NewGuid().ToString("N")[..5]
            });

            await using (var seedCtx = CatalogTestHelper.CreateSqlServerContext(dbName, migrate: false))
            {
                var inv = new InventoryService(seedCtx);
                Assert.True((await inv.ReceiveAsync(new ReceiptRequest
                {
                    WarehouseId = warehouse.Value!.Id,
                    VariantId = variantId,
                    Quantity = 10
                }, null)).IsSuccess);
            }

            var staffUser = new ApplicationUser
            {
                UserName = "conc-staff-" + Guid.NewGuid().ToString("N")[..8] + "@t.local",
                Email = "conc-staff-" + Guid.NewGuid().ToString("N")[..8] + "@t.local",
                NormalizedUserName = Guid.NewGuid().ToString("N"),
                NormalizedEmail = Guid.NewGuid().ToString("N"),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                FirstName = "Staff",
                LastName = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(staffUser);

            var customer = new Customer
            {
                FirstName = "C",
                LastName = "C",
                Email = "c-" + Guid.NewGuid().ToString("N")[..6] + "@t.local",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            async Task<Result<OrderDetailsDto>> PlaceAsync()
            {
                try
                {
                    await using var scoped = CatalogTestHelper.CreateSqlServerContext(dbName, migrate: false);
                    var service = new OrderService(scoped, new InventoryService(scoped));
                    return await service.CreateAsync(new CreateOrderRequest
                    {
                        WarehouseId = warehouse.Value!.Id,
                        CustomerId = customer.Id,
                        Items = [new CreateOrderItemRequest { ProductVariantId = variantId, Quantity = 7 }]
                    }, new ActorContext { UserId = staffUser.Id, IsStaff = true });
                }
                catch (Exception)
                {
                    // Deadlock victims / transient SQL errors count as failed reservations.
                    return Result.Conflict<OrderDetailsDto>("Concurrent reservation failed.", ResultCodes.ConcurrencyConflict);
                }
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => PlaceAsync()));
            var success = results.Count(r => r.IsSuccess);
            Assert.True(success <= 1);
            Assert.Equal(3, success + results.Count(r => r.IsFailure));

            await using var verify = CatalogTestHelper.CreateSqlServerContext(dbName, migrate: false);
            var item = await verify.Inventory.SingleAsync(i => i.WarehouseId == warehouse.Value!.Id && i.VariantId == variantId);
            Assert.True(item.QuantityReserved <= 10);
            Assert.True(item.QuantityOnHand - item.QuantityReserved >= 0);
            Assert.Equal(success * 7, item.QuantityReserved);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Reports_ExcludeCancelledFromCompletedRevenue()
    {
        var fx = await CreateFixtureAsync(20);
        await using var _ = fx.Db;

        var a = await fx.Orders.CreateAsync(BuildRequest(fx, 2), fx.Staff);
        await fx.Orders.ConfirmAsync(a.Value!.Id, fx.Staff);
        await fx.Orders.StartProcessingAsync(a.Value.Id, fx.Staff);
        await fx.Orders.CompleteAsync(a.Value.Id, fx.Staff);

        var b = await fx.Orders.CreateAsync(BuildRequest(fx, 1), fx.Staff);
        await fx.Orders.CancelAsync(b.Value!.Id, fx.Staff);

        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);
        var summary = await fx.Orders.GetPeriodSummaryAsync(from, to, null, fx.Staff);
        Assert.True(summary.IsSuccess);
        Assert.Equal(2, summary.Value!.TotalOrders);
        Assert.Equal(1, summary.Value.CompletedCount);
        Assert.Equal(1, summary.Value.CancelledCount);
        Assert.Equal(200m, summary.Value.TotalAmountCompleted);
    }
}
