# TechStore Backend

Modular monolith for **TechStore** — catalog, warehouse inventory, stock movements, and order foundation (backend API only).

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

### Phase 3 migration: `InventoryConcurrencyAndIdempotency`

Additive migration (does not drop catalog tables):

- `Inventory.RowVersion` (SQL Server `rowversion`)
- `StockMovements.QuantityBefore`, `QuantityAfter`, `TransferGroupId`
- `ProductVariants.LowStockThreshold` (nullable, check ≥ 0)
- Table `StockOperationIdempotency` with unique `IdempotencyKey`
- Supporting indexes

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
   - Adjustment: `WHERE QuantityOnHand == expected` then set target
2. Stock change + movement (+ idempotency row) run in **one transaction**
3. Transfer updates both warehouses in the **same transaction**
4. `RowVersion` is configured for optimistic concurrency on tracked updates
5. Race on stock → **409** (`insufficient_stock` / `concurrency_conflict`), not HTTP 500
6. InMemory provider (unit/integration helpers) uses a tracked-entity fallback because it cannot translate `ExecuteUpdate`; **SQL Server concurrency is proven by** `ConcurrentIssues_DoNotOversell_OnSqlServer` (LocalDB)

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

## Tests

```powershell
dotnet test TechStore.sln
```

- **Unit:** domain rules (hierarchy, inventory, slug), validators, `Result`
- **Integration:** catalog services (InMemory), inventory operations (InMemory + LocalDB concurrency), anonymous API **401**

Concurrency test requires LocalDB and creates/drops a temporary database (`TechStore_InvConcurrency_*`).

## Design notes

1. **`ApplicationUser` in Infrastructure** — Identity inheritance; domain uses `int` FKs.
2. **No generic repository / MediatR / AutoMapper.**
3. Soft deactivate (`IsActive`) preferred over hard delete when history exists.
4. Money as `decimal`; stock quantities as `int`.
5. Stock history ≠ `AuditLog`: movements are business history; audit records user/system actions where wired.
6. Reservation field exists for future order holds; Phase 3 does not implement reservation APIs.

## Next milestones

- Order placement with stock reservation
- CSV import pipeline
- PDF order confirmation metadata
- Stronger auth (JWT / refresh) if clients need non-cookie access
