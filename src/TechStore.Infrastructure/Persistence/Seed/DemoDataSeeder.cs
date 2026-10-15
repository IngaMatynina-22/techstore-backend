using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechStore.Application.Features.Settings;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent rich demo dataset for local/dev (marker: warehouse code WH-MAIN).
/// </summary>
public static class DemoDataSeeder
{
    public const string MarkerWarehouseCode = "WH-MAIN";
    private const string DemoPassword = "ChangeMe123!";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        if (await db.Warehouses.AnyAsync(w => w.Code == MarkerWarehouseCode, cancellationToken))
        {
            logger.LogInformation("Demo data already present (warehouse {Code}); skipping.", MarkerWarehouseCode);
            return;
        }

        logger.LogInformation("Seeding comprehensive demo data…");
        var now = DateTime.UtcNow;

        var admin = await EnsureUserAsync(userManager, "admin@techstore.local", "System", "Admin", ApplicationRoles.Admin, cancellationToken);
        var manager = await EnsureUserAsync(userManager, "manager@techstore.local", "Olena", "Manager", ApplicationRoles.Manager, cancellationToken);
        var manager2 = await EnsureUserAsync(userManager, "manager2@techstore.local", "Andriy", "Koval", ApplicationRoles.Manager, cancellationToken);
        var custUser1 = await EnsureUserAsync(userManager, "customer1@techstore.local", "Ivan", "Petrenko", ApplicationRoles.Customer, cancellationToken);
        var custUser2 = await EnsureUserAsync(userManager, "customer2@techstore.local", "Maria", "Shevchenko", ApplicationRoles.Customer, cancellationToken);

        var statuses = await db.OrderStatuses.ToDictionaryAsync(s => s.Code, cancellationToken);
        RequireStatus(statuses, "New", "Confirmed", "Processing", "ReadyForShipment", "Shipped", "Delivered", "Completed", "Cancelled", "Returned");

        var whMain = new Warehouse { Name = "Main Kyiv Hub", Code = MarkerWarehouseCode, Address = "Kyiv, Stoichne shose 1", IsActive = true, CreatedAt = now };
        var whEast = new Warehouse { Name = "Kharkiv East", Code = "WH-EAST", Address = "Kharkiv, Nauky Ave 12", IsActive = true, CreatedAt = now };
        var whWest = new Warehouse { Name = "Lviv West", Code = "WH-WEST", Address = "Lviv, Horodotska 100", IsActive = true, CreatedAt = now };
        db.Warehouses.AddRange(whMain, whEast, whWest);

        var brands = new[]
        {
            new Brand { Name = "Apple", Description = "Consumer electronics", WebsiteUrl = "https://www.apple.com", IsActive = true, CreatedAt = now },
            new Brand { Name = "Samsung", Description = "Mobile and displays", WebsiteUrl = "https://www.samsung.com", IsActive = true, CreatedAt = now },
            new Brand { Name = "Logitech", Description = "Peripherals", WebsiteUrl = "https://www.logitech.com", IsActive = true, CreatedAt = now },
            new Brand { Name = "Dell", Description = "PCs and monitors", WebsiteUrl = "https://www.dell.com", IsActive = true, CreatedAt = now },
            new Brand { Name = "ASUS", Description = "Laptops and components", WebsiteUrl = "https://www.asus.com", IsActive = true, CreatedAt = now },
            new Brand { Name = "Xiaomi", Description = "Value electronics", WebsiteUrl = "https://www.mi.com", IsActive = true, CreatedAt = now }
        };
        db.Brands.AddRange(brands);

        var catRoot = new Category { Name = "Electronics", Slug = "electronics", Description = "All electronics", SortOrder = 1, IsActive = true, CreatedAt = now };
        var catLaptops = new Category { Name = "Laptops", Slug = "laptops", Description = "Notebooks", SortOrder = 1, IsActive = true, CreatedAt = now, Parent = catRoot };
        var catPhones = new Category { Name = "Phones", Slug = "phones", Description = "Smartphones", SortOrder = 2, IsActive = true, CreatedAt = now, Parent = catRoot };
        var catAcc = new Category { Name = "Accessories", Slug = "accessories", Description = "Keyboards, mice, cases", SortOrder = 3, IsActive = true, CreatedAt = now, Parent = catRoot };
        var catComponents = new Category { Name = "Components", Slug = "components", Description = "PC parts", SortOrder = 2, IsActive = true, CreatedAt = now };
        var catStorage = new Category { Name = "Storage", Slug = "storage", Description = "SSD / HDD", SortOrder = 1, IsActive = true, CreatedAt = now, Parent = catComponents };
        db.Categories.AddRange(catRoot, catLaptops, catPhones, catAcc, catComponents, catStorage);

        await db.SaveChangesAsync(cancellationToken);

        var attrColor = new AttributeDefinition
        {
            CategoryId = catPhones.Id, Name = "Color", Code = "color", DataType = AttributeDataType.Select,
            IsRequired = true, IsFilterable = true, SortOrder = 1, CreatedAt = now
        };
        var attrStorage = new AttributeDefinition
        {
            CategoryId = catPhones.Id, Name = "Storage", Code = "storage", DataType = AttributeDataType.Select,
            Unit = "GB", IsRequired = true, IsFilterable = true, SortOrder = 2, CreatedAt = now
        };
        var attrRam = new AttributeDefinition
        {
            CategoryId = catLaptops.Id, Name = "RAM", Code = "ram", DataType = AttributeDataType.Select,
            Unit = "GB", IsRequired = true, IsFilterable = true, SortOrder = 1, CreatedAt = now
        };
        db.AttributeDefinitions.AddRange(attrColor, attrStorage, attrRam);
        await db.SaveChangesAsync(cancellationToken);

        var optBlack = new AttributeOption { AttributeId = attrColor.Id, Value = "Black", SortOrder = 1 };
        var optBlue = new AttributeOption { AttributeId = attrColor.Id, Value = "Blue", SortOrder = 2 };
        var opt128 = new AttributeOption { AttributeId = attrStorage.Id, Value = "128", SortOrder = 1 };
        var opt256 = new AttributeOption { AttributeId = attrStorage.Id, Value = "256", SortOrder = 2 };
        var opt512 = new AttributeOption { AttributeId = attrStorage.Id, Value = "512", SortOrder = 3 };
        var optRam16 = new AttributeOption { AttributeId = attrRam.Id, Value = "16", SortOrder = 1 };
        var optRam32 = new AttributeOption { AttributeId = attrRam.Id, Value = "32", SortOrder = 2 };
        db.AttributeOptions.AddRange(optBlack, optBlue, opt128, opt256, opt512, optRam16, optRam32);
        await db.SaveChangesAsync(cancellationToken);

        var apple = brands[0];
        var samsung = brands[1];
        var logitech = brands[2];
        var dell = brands[3];
        var asus = brands[4];
        var xiaomi = brands[5];

        var products = new List<(Product Product, List<ProductVariant> Variants)>();

        void AddProduct(Product p, params ProductVariant[] variants)
        {
            p.CreatedBy = admin.Id;
            p.CreatedAt = now;
            foreach (var v in variants)
            {
                v.CreatedAt = now;
                v.IsActive = true;
            }

            p.Variants = variants;
            db.Products.Add(p);
            products.Add((p, variants.ToList()));
        }

        AddProduct(
            new Product
            {
                CategoryId = catPhones.Id, BrandId = apple.Id, Name = "iPhone 15", Slug = "iphone-15",
                ShortDescription = "A16 Bionic smartphone", Description = "Demo iPhone 15 with dual camera.",
                ImageUrl = "/images/demo/iphone15.jpg", WarrantyMonths = 12, IsActive = true
            },
            new ProductVariant
            {
                Sku = "APL-IP15-128-BLK", Barcode = "1000000000011", VariantName = "128GB Black",
                PurchasePrice = 720m, SellingPrice = 899m, OldPrice = 949m, Weight = 0.171m, LowStockThreshold = 5
            },
            new ProductVariant
            {
                Sku = "APL-IP15-256-BLU", Barcode = "1000000000012", VariantName = "256GB Blue",
                PurchasePrice = 820m, SellingPrice = 999m, Weight = 0.171m, LowStockThreshold = 3
            });

        AddProduct(
            new Product
            {
                CategoryId = catPhones.Id, BrandId = samsung.Id, Name = "Galaxy S24", Slug = "galaxy-s24",
                ShortDescription = "Flagship Android", Description = "Demo Galaxy S24.",
                ImageUrl = "/images/demo/s24.jpg", WarrantyMonths = 24, IsActive = true
            },
            new ProductVariant
            {
                Sku = "SAM-S24-256-BLK", Barcode = "1000000000021", VariantName = "256GB Black",
                PurchasePrice = 650m, SellingPrice = 849m, Weight = 0.167m, LowStockThreshold = 8
            });

        AddProduct(
            new Product
            {
                CategoryId = catPhones.Id, BrandId = xiaomi.Id, Name = "Redmi Note 13", Slug = "redmi-note-13",
                ShortDescription = "Budget phone", Description = "Demo Redmi Note 13.",
                WarrantyMonths = 12, IsActive = true
            },
            new ProductVariant
            {
                Sku = "XIA-RN13-128-BLK", Barcode = "1000000000031", VariantName = "128GB Black",
                PurchasePrice = 180m, SellingPrice = 249m, Weight = 0.188m, LowStockThreshold = 15
            });

        AddProduct(
            new Product
            {
                CategoryId = catLaptops.Id, BrandId = dell.Id, Name = "Dell XPS 13", Slug = "dell-xps-13",
                ShortDescription = "Ultrabook", Description = "Demo Dell XPS 13.",
                ImageUrl = "/images/demo/xps13.jpg", WarrantyMonths = 24, IsActive = true
            },
            new ProductVariant
            {
                Sku = "DEL-XPS13-16", Barcode = "1000000000041", VariantName = "16GB RAM / 512SSD",
                PurchasePrice = 980m, SellingPrice = 1299m, Weight = 1.2m, LowStockThreshold = 2
            });

        AddProduct(
            new Product
            {
                CategoryId = catLaptops.Id, BrandId = asus.Id, Name = "ASUS Vivobook 15", Slug = "asus-vivobook-15",
                ShortDescription = "Everyday laptop", Description = "Demo Vivobook 15.",
                WarrantyMonths = 12, IsActive = true
            },
            new ProductVariant
            {
                Sku = "ASU-VB15-16", Barcode = "1000000000051", VariantName = "16GB RAM",
                PurchasePrice = 420m, SellingPrice = 599m, OldPrice = 649m, Weight = 1.7m, LowStockThreshold = 6
            },
            new ProductVariant
            {
                Sku = "ASU-VB15-32", Barcode = "1000000000052", VariantName = "32GB RAM",
                PurchasePrice = 520m, SellingPrice = 749m, Weight = 1.7m, LowStockThreshold = 4
            });

        AddProduct(
            new Product
            {
                CategoryId = catAcc.Id, BrandId = logitech.Id, Name = "MX Master 3S", Slug = "mx-master-3s",
                ShortDescription = "Wireless mouse", Description = "Demo Logitech MX Master 3S.",
                WarrantyMonths = 24, IsActive = true
            },
            new ProductVariant
            {
                Sku = "LOG-MX3S", Barcode = "1000000000061", VariantName = "Graphite",
                PurchasePrice = 70m, SellingPrice = 109m, Weight = 0.141m, LowStockThreshold = 20
            });

        AddProduct(
            new Product
            {
                CategoryId = catAcc.Id, BrandId = apple.Id, Name = "Magic Keyboard", Slug = "magic-keyboard",
                ShortDescription = "Apple keyboard", Description = "Demo Magic Keyboard.",
                IsActive = true
            },
            new ProductVariant
            {
                Sku = "APL-MKBD", Barcode = "1000000000071", VariantName = "US English",
                PurchasePrice = 80m, SellingPrice = 119m, Weight = 0.231m, LowStockThreshold = 10
            });

        AddProduct(
            new Product
            {
                CategoryId = catStorage.Id, BrandId = samsung.Id, Name = "990 PRO NVMe 1TB", Slug = "samsung-990-pro-1tb",
                ShortDescription = "PCIe 4.0 SSD", Description = "Demo SSD.",
                WarrantyMonths = 60, IsActive = true
            },
            new ProductVariant
            {
                Sku = "SAM-990PRO-1TB", Barcode = "1000000000081", VariantName = "1TB",
                PurchasePrice = 95m, SellingPrice = 139m, Weight = 0.01m, LowStockThreshold = 12
            });

        AddProduct(
            new Product
            {
                CategoryId = catLaptops.Id, BrandId = apple.Id, Name = "MacBook Air M3", Slug = "macbook-air-m3",
                ShortDescription = "Thin laptop", Description = "Demo MacBook Air M3.",
                ImageUrl = "/images/demo/mba-m3.jpg", WarrantyMonths = 12, IsActive = true
            },
            new ProductVariant
            {
                Sku = "APL-MBA-M3-16", Barcode = "1000000000091", VariantName = "16GB / 512SSD",
                PurchasePrice = 1100m, SellingPrice = 1499m, Weight = 1.24m, LowStockThreshold = 2
            });

        // inactive product for UI edge cases
        AddProduct(
            new Product
            {
                CategoryId = catAcc.Id, BrandId = logitech.Id, Name = "Legacy Webcam C270", Slug = "legacy-c270",
                ShortDescription = "Discontinued", Description = "Inactive demo SKU.",
                IsActive = false
            },
            new ProductVariant
            {
                Sku = "LOG-C270-OLD", Barcode = "1000000000101", VariantName = "Black",
                PurchasePrice = 15m, SellingPrice = 29m, IsActive = false, LowStockThreshold = 0
            });

        await db.SaveChangesAsync(cancellationToken);

        var vIp128 = products[0].Variants[0];
        var vIp256 = products[0].Variants[1];
        var vS24 = products[1].Variants[0];
        var vRn13 = products[2].Variants[0];
        var vXps = products[3].Variants[0];
        var vVb16 = products[4].Variants[0];
        var vVb32 = products[4].Variants[1];
        var vMx = products[5].Variants[0];
        var vMkbd = products[6].Variants[0];
        var vSsd = products[7].Variants[0];
        var vMba = products[8].Variants[0];

        db.ProductImages.AddRange(
            new ProductImage { ProductId = products[0].Product.Id, ImageUrl = "/images/demo/iphone15-1.jpg", AltText = "iPhone front", SortOrder = 1, IsPrimary = true, CreatedAt = now },
            new ProductImage { ProductId = products[0].Product.Id, ImageUrl = "/images/demo/iphone15-2.jpg", AltText = "iPhone back", SortOrder = 2, IsPrimary = false, CreatedAt = now },
            new ProductImage { ProductId = products[1].Product.Id, ImageUrl = "/images/demo/s24-1.jpg", AltText = "Galaxy S24", SortOrder = 1, IsPrimary = true, CreatedAt = now },
            new ProductImage { ProductId = products[3].Product.Id, ImageUrl = "/images/demo/xps-1.jpg", AltText = "XPS 13", SortOrder = 1, IsPrimary = true, CreatedAt = now });

        db.VariantAttributeValues.AddRange(
            new VariantAttributeValue { VariantId = vIp128.Id, AttributeId = attrColor.Id, OptionId = optBlack.Id, Value = "Black" },
            new VariantAttributeValue { VariantId = vIp128.Id, AttributeId = attrStorage.Id, OptionId = opt128.Id, Value = "128" },
            new VariantAttributeValue { VariantId = vIp256.Id, AttributeId = attrColor.Id, OptionId = optBlue.Id, Value = "Blue" },
            new VariantAttributeValue { VariantId = vIp256.Id, AttributeId = attrStorage.Id, OptionId = opt256.Id, Value = "256" },
            new VariantAttributeValue { VariantId = vS24.Id, AttributeId = attrColor.Id, OptionId = optBlack.Id, Value = "Black" },
            new VariantAttributeValue { VariantId = vS24.Id, AttributeId = attrStorage.Id, OptionId = opt256.Id, Value = "256" },
            new VariantAttributeValue { VariantId = vXps.Id, AttributeId = attrRam.Id, OptionId = optRam16.Id, Value = "16" },
            new VariantAttributeValue { VariantId = vVb16.Id, AttributeId = attrRam.Id, OptionId = optRam16.Id, Value = "16" },
            new VariantAttributeValue { VariantId = vVb32.Id, AttributeId = attrRam.Id, OptionId = optRam32.Id, Value = "32" },
            new VariantAttributeValue { VariantId = vMba.Id, AttributeId = attrRam.Id, OptionId = optRam16.Id, Value = "16" });

        var supplierA = new Supplier
        {
            Name = "TechDistribute UA", ContactPerson = "Serhiy Bondar", Email = "sales@techdistribute.ua",
            Phone = "+380441112233", Address = "Kyiv, Industrialna 8", TaxNumber = "UA31234567",
            Notes = "Primary distributor", IsActive = true, CreatedAt = now
        };
        var supplierB = new Supplier
        {
            Name = "GadgetWorld EU", ContactPerson = "Anna Nowak", Email = "orders@gadgetworld.eu",
            Phone = "+48221112233", Address = "Warsaw, Logistyczna 3", TaxNumber = "PL5252525252",
            IsActive = true, CreatedAt = now
        };
        var supplierInactive = new Supplier
        {
            Name = "OldParts LLC", ContactPerson = "N/A", Email = "old@parts.local",
            IsActive = false, CreatedAt = now.AddYears(-2)
        };
        db.Suppliers.AddRange(supplierA, supplierB, supplierInactive);
        await db.SaveChangesAsync(cancellationToken);

        db.SupplierProducts.AddRange(
            new SupplierProduct { SupplierId = supplierA.Id, VariantId = vIp128.Id, SupplierSku = "TD-IP15-128", SupplyPrice = 710m, DeliveryDays = 3, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierA.Id, VariantId = vIp256.Id, SupplierSku = "TD-IP15-256", SupplyPrice = 805m, DeliveryDays = 3, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierA.Id, VariantId = vS24.Id, SupplierSku = "TD-S24", SupplyPrice = 640m, DeliveryDays = 5, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierA.Id, VariantId = vMx.Id, SupplierSku = "TD-MX3S", SupplyPrice = 68m, DeliveryDays = 2, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierA.Id, VariantId = vSsd.Id, SupplierSku = "TD-990", SupplyPrice = 92m, DeliveryDays = 4, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierB.Id, VariantId = vXps.Id, SupplierSku = "GW-XPS13", SupplyPrice = 960m, DeliveryDays = 7, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierB.Id, VariantId = vMba.Id, SupplierSku = "GW-MBA-M3", SupplyPrice = 1080m, DeliveryDays = 10, IsPreferred = true },
            new SupplierProduct { SupplierId = supplierB.Id, VariantId = vVb16.Id, SupplierSku = "GW-VB16", SupplyPrice = 410m, DeliveryDays = 6, IsPreferred = false },
            new SupplierProduct { SupplierId = supplierB.Id, VariantId = vIp128.Id, SupplierSku = "GW-IP15", SupplyPrice = 735m, DeliveryDays = 8, IsPreferred = false });

        // Inventory: on-hand with some reserved for open orders
        var invDefs = new (Warehouse Wh, ProductVariant Var, int OnHand, int Reserved)[]
        {
            (whMain, vIp128, 40, 3), (whMain, vIp256, 25, 0), (whMain, vS24, 30, 2),
            (whMain, vRn13, 60, 0), (whMain, vXps, 8, 1), (whMain, vVb16, 20, 0),
            (whMain, vVb32, 12, 0), (whMain, vMx, 80, 5), (whMain, vMkbd, 35, 0),
            (whMain, vSsd, 50, 0), (whMain, vMba, 6, 1),
            (whEast, vIp128, 15, 0), (whEast, vS24, 18, 0), (whEast, vMx, 40, 0), (whEast, vSsd, 22, 0),
            (whWest, vIp256, 10, 0), (whWest, vVb16, 14, 0), (whWest, vMba, 4, 0), (whWest, vMkbd, 12, 0)
        };

        var inventory = new List<InventoryItem>();
        foreach (var (wh, variant, onHand, reserved) in invDefs)
        {
            inventory.Add(new InventoryItem
            {
                WarehouseId = wh.Id,
                VariantId = variant.Id,
                QuantityOnHand = onHand,
                QuantityReserved = reserved,
                UpdatedAt = now
            });
        }

        db.Inventory.AddRange(inventory);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in inventory)
        {
            db.StockMovements.Add(new StockMovement
            {
                WarehouseId = item.WarehouseId,
                VariantId = item.VariantId,
                UserId = admin.Id,
                MovementType = StockMovementType.Receipt,
                Quantity = item.QuantityOnHand,
                QuantityBefore = 0,
                QuantityAfter = item.QuantityOnHand,
                UnitCost = products.SelectMany(p => p.Variants).First(v => v.Id == item.VariantId).PurchasePrice,
                ReferenceType = "DemoSeed",
                Comment = "Initial demo receipt",
                CreatedAt = now.AddDays(-14)
            });
        }

        // sample transfer Main -> East for MX mouse
        var transferId = Guid.NewGuid();
        var mxMain = inventory.First(i => i.WarehouseId == whMain.Id && i.VariantId == vMx.Id);
        db.StockMovements.AddRange(
            new StockMovement
            {
                WarehouseId = whMain.Id, VariantId = vMx.Id, UserId = manager.Id,
                MovementType = StockMovementType.TransferOut, Quantity = -10,
                QuantityBefore = mxMain.QuantityOnHand + 10, QuantityAfter = mxMain.QuantityOnHand,
                TransferGroupId = transferId, ReferenceType = "Transfer", Comment = "Demo transfer out",
                CreatedAt = now.AddDays(-7)
            },
            new StockMovement
            {
                WarehouseId = whEast.Id, VariantId = vMx.Id, UserId = manager.Id,
                MovementType = StockMovementType.TransferIn, Quantity = 10,
                QuantityBefore = 30, QuantityAfter = 40,
                TransferGroupId = transferId, ReferenceType = "Transfer", Comment = "Demo transfer in",
                CreatedAt = now.AddDays(-7)
            });

        var customer1 = new Customer
        {
            UserId = custUser1.Id, FirstName = "Ivan", LastName = "Petrenko",
            Email = "customer1@techstore.local", Phone = "+380501111111",
            Notes = "VIP retail", IsActive = true, CreatedAt = now.AddMonths(-3)
        };
        var customer2 = new Customer
        {
            UserId = custUser2.Id, FirstName = "Maria", LastName = "Shevchenko",
            Email = "customer2@techstore.local", Phone = "+380672222222",
            IsActive = true, CreatedAt = now.AddMonths(-2)
        };
        var customer3 = new Customer
        {
            FirstName = "Guest", LastName = "Buyer", Email = "guest.buyer@example.com",
            Phone = "+380633333333", Notes = "No Identity account", IsActive = true, CreatedAt = now.AddMonths(-1)
        };
        var customerInactive = new Customer
        {
            FirstName = "Inactive", LastName = "Client", Email = "inactive@example.com",
            IsActive = false, CreatedAt = now.AddYears(-1)
        };
        db.Customers.AddRange(customer1, customer2, customer3, customerInactive);
        await db.SaveChangesAsync(cancellationToken);

        var addr1 = new CustomerAddress
        {
            CustomerId = customer1.Id, Title = "Home", RecipientName = "Ivan Petrenko", Phone = "+380501111111",
            Country = "UA", Region = "Kyiv", City = "Kyiv", Street = "Khreshchatyk", Building = "22", Apartment = "10",
            PostalCode = "01001", IsDefault = true, CreatedAt = now
        };
        var addr2 = new CustomerAddress
        {
            CustomerId = customer1.Id, Title = "Office", RecipientName = "Ivan Petrenko", Phone = "+380501111111",
            Country = "UA", Region = "Kyiv", City = "Kyiv", Street = "Baseina", Building = "5",
            PostalCode = "01004", IsDefault = false, CreatedAt = now
        };
        var addr3 = new CustomerAddress
        {
            CustomerId = customer2.Id, Title = "Home", RecipientName = "Maria Shevchenko", Phone = "+380672222222",
            Country = "UA", Region = "Lviv", City = "Lviv", Street = "Svobody", Building = "1", Apartment = "3",
            PostalCode = "79000", IsDefault = true, CreatedAt = now
        };
        db.CustomerAddresses.AddRange(addr1, addr2, addr3);
        await db.SaveChangesAsync(cancellationToken);

        OrderItem Line(ProductVariant v, int qty, decimal? unit = null)
        {
            var price = unit ?? v.SellingPrice;
            return new OrderItem
            {
                VariantId = v.Id,
                ProductName = products.First(p => p.Variants.Any(x => x.Id == v.Id)).Product.Name,
                VariantName = v.VariantName,
                Sku = v.Sku,
                Quantity = qty,
                UnitPrice = price,
                DiscountAmount = 0,
                Subtotal = price * qty
            };
        }

        Order MakeOrder(
            string number,
            Customer customer,
            CustomerAddress? shipAddr,
            string statusCode,
            Warehouse warehouse,
            bool reserved,
            DateTime created,
            params OrderItem[] items)
        {
            var sub = items.Sum(i => i.Subtotal);
            var delivery = statusCode is "Cancelled" ? 0m : 80m;
            var order = new Order
            {
                OrderNumber = number,
                CustomerId = customer.Id,
                ManagerId = manager.Id,
                StatusId = statuses[statusCode].Id,
                WarehouseId = warehouse.Id,
                ShippingAddressId = shipAddr?.Id,
                CustomerName = $"{customer.FirstName} {customer.LastName}",
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                DeliveryMethod = DeliveryMethod.Courier,
                DeliveryAddress = shipAddr is null
                    ? null
                    : $"{shipAddr.City}, {shipAddr.Street} {shipAddr.Building}",
                Comment = $"Demo order {number}",
                Subtotal = sub,
                DiscountAmount = 0,
                DeliveryCost = delivery,
                TotalAmount = sub + delivery,
                HasActiveReservation = reserved,
                CreatedAt = created,
                ConfirmedAt = statusCode is "New" or "Cancelled" ? null : created.AddHours(1),
                CompletedAt = statusCode is "Completed" or "Delivered" ? created.AddDays(5) : null,
                CancelledAt = statusCode == "Cancelled" ? created.AddHours(3) : null,
                Items = items.ToList()
            };
            return order;
        }

        var orderNew = MakeOrder("ORD-2026-0001", customer1, addr1, "New", whMain, true, now.AddDays(-1),
            Line(vIp128, 2), Line(vMx, 3));
        var orderConfirmed = MakeOrder("ORD-2026-0002", customer2, addr3, "Confirmed", whMain, true, now.AddDays(-3),
            Line(vS24, 1), Line(vMx, 2));
        var orderProcessing = MakeOrder("ORD-2026-0003", customer1, addr2, "Processing", whMain, true, now.AddDays(-5),
            Line(vXps, 1));
        var orderShipped = MakeOrder("ORD-2026-0004", customer3, null, "Shipped", whMain, false, now.AddDays(-10),
            Line(vMba, 1), Line(vMkbd, 1));
        orderShipped.DeliveryAddress = "Odesa, Deribasivska 1";
        orderShipped.HasActiveReservation = false;
        var orderCompleted = MakeOrder("ORD-2026-0005", customer2, addr3, "Completed", whMain, false, now.AddDays(-20),
            Line(vSsd, 2), Line(vMx, 1));
        var orderCancelled = MakeOrder("ORD-2026-0006", customer1, addr1, "Cancelled", whMain, false, now.AddDays(-8),
            Line(vVb32, 1));
        var orderDelivered = MakeOrder("ORD-2026-0007", customer3, null, "Delivered", whEast, false, now.AddDays(-15),
            Line(vRn13, 1));
        orderDelivered.DeliveryAddress = "Dnipro, Naberezhna 4";

        db.Orders.AddRange(orderNew, orderConfirmed, orderProcessing, orderShipped, orderCompleted, orderCancelled, orderDelivered);
        await db.SaveChangesAsync(cancellationToken);

        void Hist(Order o, string? from, string to, DateTime at, string? comment = null) =>
            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = o.Id,
                OldStatusId = from is null ? null : statuses[from].Id,
                NewStatusId = statuses[to].Id,
                ChangedBy = manager.Id,
                Comment = comment,
                ChangedAt = at
            });

        Hist(orderNew, null, "New", orderNew.CreatedAt);
        Hist(orderConfirmed, null, "New", orderConfirmed.CreatedAt);
        Hist(orderConfirmed, "New", "Confirmed", orderConfirmed.ConfirmedAt!.Value);
        Hist(orderProcessing, null, "New", orderProcessing.CreatedAt);
        Hist(orderProcessing, "New", "Confirmed", orderProcessing.CreatedAt.AddHours(2));
        Hist(orderProcessing, "Confirmed", "Processing", orderProcessing.CreatedAt.AddDays(1));
        Hist(orderShipped, null, "New", orderShipped.CreatedAt);
        Hist(orderShipped, "New", "Confirmed", orderShipped.CreatedAt.AddHours(1));
        Hist(orderShipped, "Confirmed", "Processing", orderShipped.CreatedAt.AddDays(1));
        Hist(orderShipped, "Processing", "ReadyForShipment", orderShipped.CreatedAt.AddDays(2));
        Hist(orderShipped, "ReadyForShipment", "Shipped", orderShipped.CreatedAt.AddDays(3));
        Hist(orderCompleted, null, "New", orderCompleted.CreatedAt);
        Hist(orderCompleted, "New", "Confirmed", orderCompleted.CreatedAt.AddHours(1));
        Hist(orderCompleted, "Confirmed", "Processing", orderCompleted.CreatedAt.AddDays(1));
        Hist(orderCompleted, "Processing", "Shipped", orderCompleted.CreatedAt.AddDays(2));
        Hist(orderCompleted, "Shipped", "Delivered", orderCompleted.CreatedAt.AddDays(4));
        Hist(orderCompleted, "Delivered", "Completed", orderCompleted.CompletedAt!.Value);
        Hist(orderCancelled, null, "New", orderCancelled.CreatedAt);
        Hist(orderCancelled, "New", "Cancelled", orderCancelled.CancelledAt!.Value, "Customer changed mind");
        Hist(orderDelivered, null, "New", orderDelivered.CreatedAt);
        Hist(orderDelivered, "New", "Confirmed", orderDelivered.CreatedAt.AddHours(1));
        Hist(orderDelivered, "Confirmed", "Shipped", orderDelivered.CreatedAt.AddDays(2));
        Hist(orderDelivered, "Shipped", "Delivered", orderDelivered.CompletedAt ?? orderDelivered.CreatedAt.AddDays(5));

        db.Payments.AddRange(
            new Payment
            {
                OrderId = orderConfirmed.Id, PaymentMethod = PaymentMethod.Card, PaymentStatus = PaymentStatus.Pending,
                Amount = orderConfirmed.TotalAmount, CreatedAt = orderConfirmed.CreatedAt
            },
            new Payment
            {
                OrderId = orderShipped.Id, PaymentMethod = PaymentMethod.Card, PaymentStatus = PaymentStatus.Paid,
                Amount = orderShipped.TotalAmount, TransactionReference = "TXN-SHIP-004",
                PaidAt = orderShipped.CreatedAt.AddHours(2), CreatedAt = orderShipped.CreatedAt
            },
            new Payment
            {
                OrderId = orderCompleted.Id, PaymentMethod = PaymentMethod.Cash, PaymentStatus = PaymentStatus.Paid,
                Amount = orderCompleted.TotalAmount, PaidAt = orderCompleted.CompletedAt, CreatedAt = orderCompleted.CreatedAt
            },
            new Payment
            {
                OrderId = orderCancelled.Id, PaymentMethod = PaymentMethod.Card, PaymentStatus = PaymentStatus.Refunded,
                Amount = orderCancelled.TotalAmount, Comment = "Refund after cancel", CreatedAt = orderCancelled.CreatedAt
            });

        db.Shipments.AddRange(
            new Shipment
            {
                OrderId = orderShipped.Id, AddressId = null, DeliveryService = "Nova Poshta",
                TrackingNumber = "NP59000000001", ShipmentStatus = ShipmentStatus.InTransit,
                RecipientName = orderShipped.CustomerName, RecipientPhone = orderShipped.CustomerPhone,
                DeliveryAddress = orderShipped.DeliveryAddress!, ShippedAt = orderShipped.CreatedAt.AddDays(3),
                CreatedAt = orderShipped.CreatedAt.AddDays(3)
            },
            new Shipment
            {
                OrderId = orderCompleted.Id, AddressId = addr3.Id, DeliveryService = "Ukrposhta",
                TrackingNumber = "UP22000000002", ShipmentStatus = ShipmentStatus.Delivered,
                RecipientName = orderCompleted.CustomerName, RecipientPhone = orderCompleted.CustomerPhone,
                DeliveryAddress = orderCompleted.DeliveryAddress!, ShippedAt = orderCompleted.CreatedAt.AddDays(2),
                DeliveredAt = orderCompleted.CreatedAt.AddDays(4), CreatedAt = orderCompleted.CreatedAt.AddDays(2)
            },
            new Shipment
            {
                OrderId = orderDelivered.Id, DeliveryService = "Meest", TrackingNumber = "ME33000000003",
                ShipmentStatus = ShipmentStatus.Delivered, RecipientName = orderDelivered.CustomerName,
                RecipientPhone = orderDelivered.CustomerPhone, DeliveryAddress = orderDelivered.DeliveryAddress!,
                ShippedAt = orderDelivered.CreatedAt.AddDays(2), DeliveredAt = orderDelivered.CreatedAt.AddDays(5),
                CreatedAt = orderDelivered.CreatedAt.AddDays(2)
            });

        db.OrderDocuments.AddRange(
            new OrderDocument
            {
                OrderId = orderConfirmed.Id, DocumentType = DocumentType.OrderConfirmation,
                DocumentNumber = "DOC-CONF-0002", FilePath = "/docs/demo/ord-0002-confirm.pdf",
                IssuedBy = manager.Id, IssuedAt = orderConfirmed.ConfirmedAt!.Value, CreatedAt = orderConfirmed.ConfirmedAt!.Value
            },
            new OrderDocument
            {
                OrderId = orderCompleted.Id, DocumentType = DocumentType.Invoice,
                DocumentNumber = "INV-0005", FilePath = "/docs/demo/ord-0005-invoice.pdf",
                IssuedBy = manager.Id, IssuedAt = orderCompleted.CompletedAt!.Value, CreatedAt = orderCompleted.CompletedAt!.Value
            },
            new OrderDocument
            {
                OrderId = orderShipped.Id, DocumentType = DocumentType.ShippingLabel,
                DocumentNumber = "LABEL-0004", FilePath = "/docs/demo/ord-0004-label.pdf",
                IssuedBy = manager2.Id, IssuedAt = orderShipped.CreatedAt.AddDays(3), CreatedAt = orderShipped.CreatedAt.AddDays(3)
            });

        // Purchase orders
        var poDraft = new PurchaseOrder
        {
            OrderNumber = "PO-2026-0001", SupplierId = supplierA.Id, WarehouseId = whMain.Id,
            Status = PurchaseOrderStatus.Draft, ExpectedDeliveryDate = now.AddDays(10),
            Notes = "Restock phones", CreatedByUserId = manager.Id, CreatedAt = now.AddDays(-2),
            Items =
            [
                new PurchaseOrderItem
                {
                    VariantId = vIp128.Id, SkuSnapshot = vIp128.Sku, ProductNameSnapshot = "iPhone 15",
                    OrderedQuantity = 20, ReceivedQuantity = 0, UnitCost = 710m, LineTotal = 14200m
                },
                new PurchaseOrderItem
                {
                    VariantId = vS24.Id, SkuSnapshot = vS24.Sku, ProductNameSnapshot = "Galaxy S24",
                    OrderedQuantity = 15, ReceivedQuantity = 0, UnitCost = 640m, LineTotal = 9600m
                }
            ],
            StatusHistory =
            [
                new PurchaseOrderStatusHistory
                {
                    NewStatus = PurchaseOrderStatus.Draft, ChangedBy = manager.Id,
                    Comment = "Created", ChangedAt = now.AddDays(-2)
                }
            ]
        };

        var poConfirmed = new PurchaseOrder
        {
            OrderNumber = "PO-2026-0002", SupplierId = supplierB.Id, WarehouseId = whMain.Id,
            Status = PurchaseOrderStatus.Confirmed, ExpectedDeliveryDate = now.AddDays(5),
            CreatedByUserId = admin.Id, ConfirmedAt = now.AddDays(-4), CreatedAt = now.AddDays(-5),
            Items =
            [
                new PurchaseOrderItem
                {
                    VariantId = vMba.Id, SkuSnapshot = vMba.Sku, ProductNameSnapshot = "MacBook Air M3",
                    OrderedQuantity = 5, ReceivedQuantity = 0, UnitCost = 1080m, LineTotal = 5400m
                }
            ],
            StatusHistory =
            [
                new PurchaseOrderStatusHistory { NewStatus = PurchaseOrderStatus.Draft, ChangedBy = admin.Id, ChangedAt = now.AddDays(-5) },
                new PurchaseOrderStatusHistory
                {
                    OldStatus = PurchaseOrderStatus.Draft, NewStatus = PurchaseOrderStatus.Confirmed,
                    ChangedBy = admin.Id, Comment = "Sent to supplier", ChangedAt = now.AddDays(-4)
                }
            ]
        };

        var poPartial = new PurchaseOrder
        {
            OrderNumber = "PO-2026-0003", SupplierId = supplierA.Id, WarehouseId = whEast.Id,
            Status = PurchaseOrderStatus.PartiallyReceived, ExpectedDeliveryDate = now.AddDays(-1),
            CreatedByUserId = manager.Id, ConfirmedAt = now.AddDays(-12), CreatedAt = now.AddDays(-14),
            Items =
            [
                new PurchaseOrderItem
                {
                    VariantId = vMx.Id, SkuSnapshot = vMx.Sku, ProductNameSnapshot = "MX Master 3S",
                    OrderedQuantity = 50, ReceivedQuantity = 20, UnitCost = 68m, LineTotal = 3400m
                },
                new PurchaseOrderItem
                {
                    VariantId = vSsd.Id, SkuSnapshot = vSsd.Sku, ProductNameSnapshot = "990 PRO NVMe 1TB",
                    OrderedQuantity = 30, ReceivedQuantity = 30, UnitCost = 92m, LineTotal = 2760m
                }
            ],
            StatusHistory =
            [
                new PurchaseOrderStatusHistory { NewStatus = PurchaseOrderStatus.Draft, ChangedBy = manager.Id, ChangedAt = now.AddDays(-14) },
                new PurchaseOrderStatusHistory
                {
                    OldStatus = PurchaseOrderStatus.Draft, NewStatus = PurchaseOrderStatus.Confirmed,
                    ChangedBy = manager.Id, ChangedAt = now.AddDays(-12)
                },
                new PurchaseOrderStatusHistory
                {
                    OldStatus = PurchaseOrderStatus.Confirmed, NewStatus = PurchaseOrderStatus.PartiallyReceived,
                    ChangedBy = manager2.Id, Comment = "Partial receipt", ChangedAt = now.AddDays(-3)
                }
            ]
        };

        var poReceived = new PurchaseOrder
        {
            OrderNumber = "PO-2026-0004", SupplierId = supplierA.Id, WarehouseId = whMain.Id,
            Status = PurchaseOrderStatus.Received, ExpectedDeliveryDate = now.AddDays(-20),
            CreatedByUserId = manager.Id, ConfirmedAt = now.AddDays(-25), FullyReceivedAt = now.AddDays(-18),
            CreatedAt = now.AddDays(-28),
            Items =
            [
                new PurchaseOrderItem
                {
                    VariantId = vVb16.Id, SkuSnapshot = vVb16.Sku, ProductNameSnapshot = "ASUS Vivobook 15",
                    OrderedQuantity = 10, ReceivedQuantity = 10, UnitCost = 410m, LineTotal = 4100m
                }
            ],
            StatusHistory =
            [
                new PurchaseOrderStatusHistory { NewStatus = PurchaseOrderStatus.Draft, ChangedBy = manager.Id, ChangedAt = now.AddDays(-28) },
                new PurchaseOrderStatusHistory
                {
                    OldStatus = PurchaseOrderStatus.Draft, NewStatus = PurchaseOrderStatus.Confirmed,
                    ChangedBy = manager.Id, ChangedAt = now.AddDays(-25)
                },
                new PurchaseOrderStatusHistory
                {
                    OldStatus = PurchaseOrderStatus.Confirmed, NewStatus = PurchaseOrderStatus.Received,
                    ChangedBy = manager.Id, Comment = "Fully received", ChangedAt = now.AddDays(-18)
                }
            ]
        };

        var poCancelled = new PurchaseOrder
        {
            OrderNumber = "PO-2026-0005", SupplierId = supplierB.Id, WarehouseId = whWest.Id,
            Status = PurchaseOrderStatus.Cancelled, CreatedByUserId = admin.Id,
            CancelledAt = now.AddDays(-6), CreatedAt = now.AddDays(-9),
            Notes = "Supplier delay — cancelled",
            Items =
            [
                new PurchaseOrderItem
                {
                    VariantId = vXps.Id, SkuSnapshot = vXps.Sku, ProductNameSnapshot = "Dell XPS 13",
                    OrderedQuantity = 3, ReceivedQuantity = 0, UnitCost = 960m, LineTotal = 2880m
                }
            ],
            StatusHistory =
            [
                new PurchaseOrderStatusHistory { NewStatus = PurchaseOrderStatus.Draft, ChangedBy = admin.Id, ChangedAt = now.AddDays(-9) },
                new PurchaseOrderStatusHistory
                {
                    OldStatus = PurchaseOrderStatus.Draft, NewStatus = PurchaseOrderStatus.Cancelled,
                    ChangedBy = admin.Id, Comment = "Cancelled", ChangedAt = now.AddDays(-6)
                }
            ]
        };

        db.PurchaseOrders.AddRange(poDraft, poConfirmed, poPartial, poReceived, poCancelled);

        // Returns against completed order
        var completedItem = orderCompleted.Items.First();
        var retRequested = new ReturnRequest
        {
            ReturnNumber = "RET-2026-0001", OrderId = orderCompleted.Id, CustomerId = customer2.Id,
            WarehouseId = whMain.Id, Status = ReturnRequestStatus.Requested,
            CustomerComment = "Wrong color preference", RequestedByUserId = custUser2.Id,
            CreatedAt = now.AddDays(-2),
            Items =
            [
                new ReturnItem
                {
                    OrderItemId = completedItem.Id, VariantId = completedItem.VariantId,
                    SkuSnapshot = completedItem.Sku, ProductNameSnapshot = completedItem.ProductName,
                    Quantity = 1, UnitPriceSnapshot = completedItem.UnitPrice,
                    LineTotal = completedItem.UnitPrice, ReasonCode = "WRONG_ITEM",
                    Disposition = ReturnDisposition.RestockSellable
                }
            ],
            StatusHistory =
            [
                new ReturnStatusHistory
                {
                    NewStatus = ReturnRequestStatus.Requested, ChangedBy = custUser2.Id,
                    Comment = "Customer submitted", ChangedAt = now.AddDays(-2)
                }
            ]
        };

        var deliveredItem = orderDelivered.Items.First();
        var retApproved = new ReturnRequest
        {
            ReturnNumber = "RET-2026-0002", OrderId = orderDelivered.Id, CustomerId = customer3.Id,
            WarehouseId = whEast.Id, Status = ReturnRequestStatus.Approved,
            CustomerComment = "Does not charge", StaffComment = "Approved for inspection",
            RequestedByUserId = manager.Id, ReviewedByUserId = manager.Id,
            ApprovedAt = now.AddDays(-4), CreatedAt = now.AddDays(-5),
            Items =
            [
                new ReturnItem
                {
                    OrderItemId = deliveredItem.Id, VariantId = deliveredItem.VariantId,
                    SkuSnapshot = deliveredItem.Sku, ProductNameSnapshot = deliveredItem.ProductName,
                    Quantity = 1, UnitPriceSnapshot = deliveredItem.UnitPrice,
                    LineTotal = deliveredItem.UnitPrice, ReasonCode = "DEFECT",
                    Disposition = ReturnDisposition.QuarantineDamaged
                }
            ],
            StatusHistory =
            [
                new ReturnStatusHistory { NewStatus = ReturnRequestStatus.Requested, ChangedBy = manager.Id, ChangedAt = now.AddDays(-5) },
                new ReturnStatusHistory
                {
                    OldStatus = ReturnRequestStatus.Requested, NewStatus = ReturnRequestStatus.Approved,
                    ChangedBy = manager.Id, Comment = "Approved", ChangedAt = now.AddDays(-4)
                }
            ]
        };

        var retRejected = new ReturnRequest
        {
            ReturnNumber = "RET-2026-0003", OrderId = orderCompleted.Id, CustomerId = customer2.Id,
            WarehouseId = whMain.Id, Status = ReturnRequestStatus.Rejected,
            CustomerComment = "Changed mind after 30 days", StaffComment = "Outside return window",
            RequestedByUserId = custUser2.Id, ReviewedByUserId = admin.Id,
            RejectedAt = now.AddDays(-1), CreatedAt = now.AddDays(-1),
            Items =
            [
                new ReturnItem
                {
                    OrderItemId = orderCompleted.Items.Skip(1).First().Id,
                    VariantId = orderCompleted.Items.Skip(1).First().VariantId,
                    SkuSnapshot = orderCompleted.Items.Skip(1).First().Sku,
                    ProductNameSnapshot = orderCompleted.Items.Skip(1).First().ProductName,
                    Quantity = 1,
                    UnitPriceSnapshot = orderCompleted.Items.Skip(1).First().UnitPrice,
                    LineTotal = orderCompleted.Items.Skip(1).First().UnitPrice,
                    ReasonCode = "LATE", Disposition = ReturnDisposition.DoNotRestock
                }
            ],
            StatusHistory =
            [
                new ReturnStatusHistory { NewStatus = ReturnRequestStatus.Requested, ChangedBy = custUser2.Id, ChangedAt = now.AddDays(-1) },
                new ReturnStatusHistory
                {
                    OldStatus = ReturnRequestStatus.Requested, NewStatus = ReturnRequestStatus.Rejected,
                    ChangedBy = admin.Id, Comment = "Rejected", ChangedAt = now.AddDays(-1).AddHours(2)
                }
            ]
        };

        db.ReturnRequests.AddRange(retRequested, retApproved, retRejected);

        // Inventory count session (draft + in progress with lines)
        await db.Entry(mxMain).ReloadAsync(cancellationToken);
        var countSession = new InventoryCountSession
        {
            SessionNumber = "CNT-2026-0001", WarehouseId = whMain.Id,
            Status = InventoryCountStatus.InProgress, CreatedByUserId = manager.Id,
            SnapshotTakenAt = now.AddHours(-6), Notes = "Spot check mice & SSD", CreatedAt = now.AddHours(-6),
            Lines =
            [
                new InventoryCountLine
                {
                    VariantId = vMx.Id, InventoryItemId = mxMain.Id,
                    ExpectedQuantity = mxMain.QuantityOnHand, ActualQuantity = mxMain.QuantityOnHand - 1,
                    ExpectedRowVersion = mxMain.RowVersion
                }
            ]
        };
        var ssdMain = inventory.First(i => i.WarehouseId == whMain.Id && i.VariantId == vSsd.Id);
        await db.Entry(ssdMain).ReloadAsync(cancellationToken);
        countSession.Lines.Add(new InventoryCountLine
        {
            VariantId = vSsd.Id, InventoryItemId = ssdMain.Id,
            ExpectedQuantity = ssdMain.QuantityOnHand, ActualQuantity = ssdMain.QuantityOnHand,
            ExpectedRowVersion = ssdMain.RowVersion
        });

        var countDraft = new InventoryCountSession
        {
            SessionNumber = "CNT-2026-0002", WarehouseId = whWest.Id,
            Status = InventoryCountStatus.Draft, CreatedByUserId = manager2.Id,
            Notes = "Not started", CreatedAt = now.AddHours(-1)
        };
        db.InventoryCountSessions.AddRange(countSession, countDraft);

        // Settings
        db.ApplicationSettings.AddRange(
            new ApplicationSetting
            {
                SettingKey = AllowedSettings.StoreDisplayName, SettingValue = "TechStore Demo",
                Description = "Public store name", UpdatedBy = admin.Id, UpdatedAt = now
            },
            new ApplicationSetting
            {
                SettingKey = AllowedSettings.CurrencyCode, SettingValue = "UAH",
                Description = "Display currency", UpdatedBy = admin.Id, UpdatedAt = now
            },
            new ApplicationSetting
            {
                SettingKey = AllowedSettings.DefaultLowStockThreshold, SettingValue = "5",
                Description = "Default low stock", UpdatedBy = admin.Id, UpdatedAt = now
            },
            new ApplicationSetting
            {
                SettingKey = AllowedSettings.MaxImportFileBytes, SettingValue = "2097152",
                Description = "2 MB import limit", UpdatedBy = admin.Id, UpdatedAt = now
            },
            new ApplicationSetting
            {
                SettingKey = AllowedSettings.DocumentIssuerName, SettingValue = "TechStore LLC",
                Description = "Invoice issuer", UpdatedBy = admin.Id, UpdatedAt = now
            });

        // Import batch sample
        var importBatch = new ImportBatch
        {
            UserId = manager.Id,
            FileName = "demo-products.csv",
            FilePath = "/imports/demo/demo-products.csv",
            ImportType = ImportType.Products,
            Status = ImportStatus.CompletedWithErrors,
            Mode = "upsertbysku",
            TotalRows = 3,
            SuccessfulRows = 2,
            FailedRows = 1,
            CreatedAt = now.AddDays(-3),
            CompletedAt = now.AddDays(-3).AddMinutes(2),
            Rows =
            [
                new ImportRow
                {
                    RowNumber = 1, RowData = "sku,name\nDEMO-OK-1,Demo", Status = ImportRowStatus.Success,
                    ProductId = products[5].Product.Id, ProcessedAt = now.AddDays(-3)
                },
                new ImportRow
                {
                    RowNumber = 2, RowData = "sku,name\nDEMO-OK-2,Demo2", Status = ImportRowStatus.Success,
                    ProcessedAt = now.AddDays(-3)
                },
                new ImportRow
                {
                    RowNumber = 3, RowData = "bad-row", Status = ImportRowStatus.Failed,
                    ErrorMessage = "Missing required column sku", ProcessedAt = now.AddDays(-3)
                }
            ]
        };
        db.ImportBatches.Add(importBatch);

        db.AuditLogs.AddRange(
            new AuditLog
            {
                UserId = admin.Id, EntityType = "Product", EntityId = products[0].Product.Id.ToString(),
                Action = AuditAction.Create, NewValues = "{\"name\":\"iPhone 15\"}", IpAddress = "127.0.0.1",
                CreatedAt = now.AddDays(-14)
            },
            new AuditLog
            {
                UserId = manager.Id, EntityType = "Order", EntityId = "pending",
                Action = AuditAction.Update, OldValues = "{\"status\":\"New\"}", NewValues = "{\"status\":\"Confirmed\"}",
                IpAddress = "127.0.0.1", CreatedAt = orderConfirmed.ConfirmedAt!.Value
            },
            new AuditLog
            {
                UserId = admin.Id, EntityType = "ApplicationSetting", EntityId = AllowedSettings.CurrencyCode,
                Action = AuditAction.Update, NewValues = "{\"value\":\"UAH\"}", CreatedAt = now
            });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Demo data seeded: warehouses={W}, products={P}, variants={V}, orders={O}, POs={Po}, returns={R}",
            3, products.Count, products.Sum(p => p.Variants.Count), 7, 5, 3);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string firstName,
        string lastName,
        string role,
        CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, role))
            {
                await userManager.AddToRoleAsync(existing, role);
            }

            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create demo user {email}: {errors}");
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static void RequireStatus(Dictionary<string, OrderStatus> statuses, params string[] codes)
    {
        foreach (var code in codes)
        {
            if (!statuses.ContainsKey(code))
            {
                throw new InvalidOperationException($"Order status '{code}' is missing. Run base DbSeeder first.");
            }
        }
    }
}
