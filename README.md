# TechStore Backend

Modular monolith for **TechStore** — catalog, warehouse inventory, stock movements, and order management (backend API only).

## Stack

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core + SQL Server (LocalDB for development)
- ASP.NET Core Identity (`int` keys, cookie auth for API)
- xUnit unit and integration tests

## Solution structure

```text
TechStore/
├── TechStore.sln
├── src/
│   ├── TechStore.Web/            # HTTP API host, auth, DI composition
│   ├── TechStore.Application/    # Contracts, DTOs, Result
│   ├── TechStore.Domain/         # Entities, enums, domain rules
│   └── TechStore.Infrastructure/ # EF Core, Identity, services, migrations
└── tests/
    ├── TechStore.UnitTests/
    └── TechStore.IntegrationTests/
```

Dependency direction: `Web → Application/Infrastructure → Domain` (Infrastructure also references Application).

There is **no frontend**: no Razor Views, Bootstrap UI, or static SPA. `wwwroot` contains only a placeholder so the Web SDK host can start.

## Prerequisites

- [.NET SDK 8](https://dotnet.microsoft.com/download)
- SQL Server or LocalDB (`(localdb)\mssqllocaldb`)
- Optional: `dotnet-ef` tool (`dotnet tool install --global dotnet-ef --version 8.0.11`)

## Configuration

Connection string lives in `src/TechStore.Web/appsettings.json` / `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TechStore_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Database": {
    "SeedOnStartup": true
  }
}
```

**Do not commit real passwords or production secrets.** Prefer User Secrets or environment variables.

## Database migrations

```powershell
dotnet ef migrations add <Name> `
  --project src/TechStore.Infrastructure `
  --startup-project src/TechStore.Web `
  --output-dir Persistence/Migrations

dotnet ef database update `
  --project src/TechStore.Infrastructure `
  --startup-project src/TechStore.Web
```

On startup (when `Database:SeedOnStartup` is `true`), the app applies migrations and seeds:

- Roles: `Admin`, `Manager`, `Customer`
- Order statuses
- Seeded admin user from `Seed:AdminUser` (no public admin registration)

### Migrations (additive)

**Phase 3 — `InventoryConcurrencyAndIdempotency`:** `Inventory.RowVersion`, movement before/after + `TransferGroupId`, `LowStockThreshold`, `StockOperationIdempotency`.

**Phase 4 — `OrderReservationAndCustomerUserLink`:** `Customers.UserId` (unique filtered), `Orders.WarehouseId`, `ShippingAddressId`, `HasActiveReservation`, table `OrderOperationIdempotency` (unique per user + key).

## Run

```powershell
dotnet restore TechStore.sln
dotnet build TechStore.sln
dotnet run --project src/TechStore.Web
```

## Authentication

Cookie-based Identity auth. Unauthenticated API calls receive **401**; authenticated users without Admin/Manager receive **403**.

```http
POST /api/account/login
Content-Type: application/json

{ "email": "admin@techstore.local", "password": "ChangeMe123!", "rememberMe": false }
```

```http
POST /api/account/logout
GET  /api/account/me
```

Default seeded admin (change immediately in production):

```json
"Seed": {
  "AdminUser": {
    "Email": "admin@techstore.local",
    "Password": "ChangeMe123!"
  }
}
```

Catalog and inventory APIs require roles **Admin** or **Manager**. `Customer` cannot manage warehouses or stock.

---

## Catalog API (Phase 2, API-only)

| Method | Route | Purpose |
|--------|-------|---------|
| GET/POST | `/api/categories` | List / create |
| GET/PUT/DELETE | `/api/categories/{id}` | Details / update / deactivate |
| GET/POST | `/api/brands` | List / create |
| GET/PUT/DELETE | `/api/brands/{id}` | Details / update / deactivate |
| GET/POST | `/api/products` | Paged list / create |
| GET/PUT/DELETE | `/api/products/{id}` | Details / update / deactivate |
| GET/POST | `/api/attributes` | Attribute definitions |
| PUT | `/api/attributes/{id}` | Update definition |
| PUT | `/api/attributes/variants/{variantId}` | Set variant attribute values |

---

## Inventory module (Phase 3)

### Stock model

- Balance key: **Warehouse + ProductVariant** (unique index)
- `QuantityOnHand` — physical stock
- `QuantityReserved` — reserved (field present; reservation workflow not implemented yet)
- `AvailableQuantity = QuantityOnHand - QuantityReserved`
- Negative on-hand is forbidden (business rules + DB checks)

### Movement types

Operations write `StockMovement` history (not deleted via API):

| Operation | Effect |
|-----------|--------|
| Receipt | Increase on-hand |
| Issue (write-off) | Decrease if available ≥ qty |
| Adjustment | Set target on-hand; server computes delta; zero delta → `no_change` |
| Transfer | Atomic source −qty / destination +qty; both rows share `TransferGroupId` |

### Concurrency strategy

1. **Conditional atomic UPDATE** via EF `ExecuteUpdateAsync` on SQL Server:
   - Issue / transfer-out: `WHERE AvailableQuantity >= need` then decrement
   - Adjustment: `WHERE QuantityOnHand == expected AND QuantityReserved <= target`
   - Reserve: `WHERE Available >= qty` then `QuantityReserved += qty`
   - Fulfill: `WHERE OnHand >= qty AND Reserved >= qty` then both decrement
2. Stock change + movement (+ idempotency row) run in **one transaction** wrapped in `CreateExecutionStrategy()` (required with `EnableRetryOnFailure`)
3. Transfer updates both warehouses in the **same transaction**
4. `RowVersion` is configured for optimistic concurrency on tracked updates
5. Race on stock → **409** (`insufficient_stock` / `concurrency_conflict`), not HTTP 500
6. Movement `QuantityBefore`/`QuantityAfter` are derived from the post-update on-hand read
7. InMemory fallback exists for tests; SQL Server concurrency is proven by LocalDB tests

### Idempotency

Optional `idempotencyKey` on receipt / issue / adjustment / transfer:

- Unique in `StockOperationIdempotency`
- Same key + same payload → replay stored response (`replayFromIdempotency: true`), no second stock change
- Same key + different payload → **409** `idempotency_conflict`
- Key, movement, and stock update are transactional

### Low stock

Optional `ProductVariant.LowStockThreshold`. Endpoints:

- `GET /api/inventory/low-stock`
- `GET /api/inventory/out-of-stock`
- `PUT /api/inventory/variants/{variantId}/low-stock-threshold`

A SKU is “low stock” only when a threshold is set and on-hand ≤ threshold.

### Reports (backend only)

| Route | Content |
|-------|---------|
| `GET /api/warehouses/{id}/summary` | SKU counts, units on hand, low/zero stock counts |
| `GET /api/warehouses/{id}/valuation` | Stock value using current `PurchasePrice` |
| `GET /api/stock-movements/reports/period` | Aggregates by movement type for a UTC range |

**Valuation limitation:** estimation by current stored purchase price — **not** FIFO or average cost. Variants without purchase price are excluded from the valued amount; the response includes counts of valued vs skipped lines.

### Inventory API routes

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/warehouses` | List warehouses |
| GET | `/api/warehouses/{id}` | Details |
| POST | `/api/warehouses` | Create |
| PUT | `/api/warehouses/{id}` | Update |
| DELETE | `/api/warehouses/{id}` | Soft deactivate |
| GET | `/api/warehouses/{id}/summary` | Warehouse summary |
| GET | `/api/warehouses/{id}/valuation` | Inventory valuation |
| GET | `/api/inventory` | Paged balances (filters/sort) |
| GET | `/api/inventory/{warehouseId}/{variantId}` | Single balance |
| GET | `/api/inventory/low-stock` | Below threshold |
| GET | `/api/inventory/out-of-stock` | Zero on-hand |
| PUT | `/api/inventory/variants/{variantId}/low-stock-threshold` | Set threshold |
| GET | `/api/stock-movements` | Paged history |
| GET | `/api/stock-movements/{id}` | Movement details |
| GET | `/api/stock-movements/reports/period` | Period aggregates |
| POST | `/api/stock-movements/receipts` | Receipt |
| POST | `/api/stock-movements/issues` | Issue / write-off |
| POST | `/api/stock-movements/adjustments` | Inventory count adjustment |
| POST | `/api/stock-movements/transfers` | Inter-warehouse transfer |

There is **no** endpoint to set `QuantityOnHand` directly.

### Example JSON

**Receipt**

```http
POST /api/stock-movements/receipts
```

```json
{
  "warehouseId": 1,
  "variantId": 10,
  "quantity": 25,
  "comment": "PO-1042",
  "documentNumber": "IN-1042",
  "unitCost": 49.90,
  "idempotencyKey": "receipt-po-1042-v10"
}
```

**Issue**

```json
{
  "warehouseId": 1,
  "variantId": 10,
  "quantity": 3,
  "comment": "Damaged units",
  "idempotencyKey": "issue-2026-10-13-1"
}
```

**Adjustment** (client sends **target** quantity; server computes delta)

```json
{
  "warehouseId": 1,
  "variantId": 10,
  "targetQuantityOnHand": 9,
  "reason": "Cycle count 2026-10-13",
  "idempotencyKey": "adj-cc-2026-10-13-10"
}
```

**Transfer**

```json
{
  "sourceWarehouseId": 1,
  "destinationWarehouseId": 2,
  "variantId": 10,
  "quantity": 5,
  "comment": "Rebalance",
  "idempotencyKey": "xfer-1-2-10-5"
}
```

### Error semantics

| HTTP | When |
|------|------|
| 400 | Validation / bad input |
| 401 | Not authenticated |
| 403 | Authenticated but not Admin/Manager |
| 404 | Warehouse / variant / movement missing |
| 409 | Insufficient stock, concurrency conflict, idempotency conflict |
| 200 + `no_change` | Adjustment with zero delta |

Problem Details style responses via `ApiControllerBase` / `AddProblemDetails()`.

---

## Orders module (Phase 4)

### Warehouse policy

Each order targets a **single fulfillment warehouse** (`warehouseId` required on create). All line reservations and fulfillment use that warehouse. Multi-warehouse split shipping is out of scope.

### Available quantity

`Available = QuantityOnHand - QuantityReserved`

Create order atomically reserves all lines. If any line fails, prior reserves are released and the draft order is discarded (no partial order).

### Duplicate lines

Repeated `productVariantId` values in one request are **aggregated** before stock checks and pricing.

### Prices

Unit prices come from `ProductVariant.SellingPrice` on the server. Snapshots are stored on `OrderItem` (`ProductName`, `Sku`, `UnitPrice`, `Subtotal`). Later catalog price changes do not alter historical orders.

### Status lifecycle (seeded codes)

| From | Allowed to |
|------|------------|
| New | Confirmed, Cancelled |
| Confirmed | Processing, Cancelled |
| Processing | ReadyForShipment, Completed, Cancelled |
| ReadyForShipment | Shipped, Completed, Cancelled |
| Shipped | Delivered |
| Delivered | Completed |

API shortcuts:

- `POST .../confirm` — New → Confirmed
- `POST .../start-processing` — Confirmed → Processing
- `POST .../complete` — Processing|ReadyForShipment → Completed **and** fulfill stock
- `POST .../cancel` — release reservation once (idempotent if already Cancelled)

**Fulfillment:** for each line, `OnHand -= qty` and `Reserved -= qty` together; `StockMovement` type `Sale` with `ReferenceType=Order`, `ReferenceId=orderId`.

**Cancel before fulfill:** `Reserved -= qty` only; on-hand unchanged; movement type `ReleaseReservation`.

Customers may cancel only **New** or **Confirmed** orders they own. Cancel after shipment/completion is rejected (`invalid_transition`).

### Order idempotency

Optional `idempotencyKey` on create, unique per `(UserId, IdempotencyKey)`:

- Same key + same payload → replay created order (`replayFromIdempotency: true`)
- Same key + different payload → **409**
- Key scoped to the authenticated user (cannot replay another user’s key)

### Order API

| Method | Route | Roles |
|--------|-------|--------|
| POST | `/api/orders` | Admin, Manager, Customer |
| GET | `/api/orders` | Admin, Manager, Customer (own only) |
| GET | `/api/orders/{id}` | owner or staff |
| GET | `/api/orders/{id}/items` | owner or staff |
| GET | `/api/orders/{id}/history` | owner or staff |
| POST | `/api/orders/{id}/confirm` | Admin, Manager |
| POST | `/api/orders/{id}/start-processing` | Admin, Manager |
| POST | `/api/orders/{id}/complete` | Admin, Manager |
| POST | `/api/orders/{id}/cancel` | owner (limited) or staff |
| GET | `/api/orders/reports/summary` | Admin, Manager |
| GET | `/api/orders/reports/by-period` | Admin, Manager |

**Create example**

```json
{
  "warehouseId": 1,
  "shippingAddressId": 4,
  "customerComment": "Call before delivery",
  "deliveryMethod": "Courier",
  "idempotencyKey": "checkout-session-42",
  "customerId": 3,
  "items": [
    { "productVariantId": 12, "quantity": 2 },
    { "productVariantId": 25, "quantity": 1 }
  ]
}
```

`customerId` is **required for staff** and **ignored for Customer** (owner comes from `Customer.UserId` ↔ Identity).

**Reports:** cancelled orders are counted separately; `totalAmountCompleted` includes only `Completed` status. Range limited to 366 days. Aggregates use historical `Order.TotalAmount`, not current catalog prices.

### Customer ↔ Identity

`Customer.UserId` (nullable, unique when set) links a shopper Identity account to the CRM customer. Customers without a linked profile cannot create orders.

---

## Tests

```powershell
dotnet test TechStore.sln
```

- **Unit:** domain rules (hierarchy, inventory, order transitions, slug), validators, `Result`
- **Integration:** catalog, inventory, orders (InMemory + LocalDB concurrency), auth matrix (anonymous 401, Customer 403 on staff routes, Manager OK)

LocalDB concurrency DBs: `TechStore_InvConcurrency_*`, `TechStore_OrdConcurrency_*`.

## Design notes

1. **`ApplicationUser` in Infrastructure** — Identity inheritance; domain uses `int` FKs.
2. **No generic repository / MediatR / AutoMapper.**
3. Soft deactivate (`IsActive`) preferred over hard delete when history exists.
4. Money as `decimal`; stock quantities as `int`.
5. Stock history ≠ `AuditLog`.
6. Payments/Shipments entities exist but are not integrated in Phase 4 (no fake payments/carriers).
7. Returned status exists in seed but has no API transition yet.

## Next milestones

- Multi-warehouse allocation / reservation ledger per line
- Returns (`Returned`) with stock restock
- Payment and shipment workflows
- CSV import pipeline
- JWT if non-cookie clients are required

