# TechStore Backend

Modular monolith foundation for **TechStore** — inventory, orders, stock control, and CSV import preparation.

## Stack

- .NET 8 / ASP.NET Core MVC
- Entity Framework Core + SQL Server
- ASP.NET Core Identity (`int` keys)
- xUnit tests

## Solution structure

```text
TechStore/
├── TechStore.sln
├── src/
│   ├── TechStore.Web/            # MVC host, Razor Views, DI composition
│   ├── TechStore.Application/    # Application services, DTOs, contracts
│   ├── TechStore.Domain/         # Entities, enums, domain rules
│   └── TechStore.Infrastructure/ # EF Core, Identity, seed, adapters
└── tests/
    ├── TechStore.UnitTests/
    └── TechStore.IntegrationTests/
```

Dependency direction: `Web → Application/Infrastructure → Domain` (Infrastructure also references Application).

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

**Do not commit real passwords or production secrets.** Prefer User Secrets or environment variables:

```powershell
cd src/TechStore.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=TechStore;User Id=...;Password=...;TrustServerCertificate=True"
```

Or:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=.;Database=TechStore;Trusted_Connection=True;TrustServerCertificate=True"
```

SQL authentication example (local only, not for git):

```text
Server=localhost,1433;Database=TechStore;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true
```

## Database migrations

From the repository root:

```powershell
# Create a new migration (when the model changes)
dotnet ef migrations add <Name> `
  --project src/TechStore.Infrastructure `
  --startup-project src/TechStore.Web `
  --output-dir Persistence/Migrations

# Apply migrations to the configured database
dotnet ef database update `
  --project src/TechStore.Infrastructure `
  --startup-project src/TechStore.Web
```

On application startup (when `Database:SeedOnStartup` is `true`), the app also runs migrations and seeds:

- Identity roles: `Admin`, `Manager`, `Customer`
- Order statuses: `New`, `Confirmed`, `Processing`, `ReadyForShipment`, `Shipped`, `Delivered`, `Completed`, `Cancelled`, `Returned`

In Development, a failed DB connection is logged and does not crash the host so you can still inspect the UI shell. In non-Development environments the failure is rethrown.

## Run

```powershell
dotnet restore TechStore.sln
dotnet build TechStore.sln
dotnet run --project src/TechStore.Web
```

Default MVC route: `/` → `HomeController.Index`.

## Tests

```powershell
dotnet test TechStore.sln
```

- Unit tests cover domain constants and application `Result` helpers.
- Integration tests use EF Core InMemory against `ApplicationDbContext` (no SQL Server required).

## Design notes / compromises

1. **`ApplicationUser` lives in Infrastructure** because it inherits ASP.NET Core Identity types. Domain entities reference users via `int` foreign keys; Fluent API wires FKs to `ApplicationUser`.
2. **No generic repository** — services will use `ApplicationDbContext` / `DbSet` directly.
3. **Soft deactivation** (`IsActive`) is preferred over hard deletes for catalog entities that participate in order history.
4. **Order line snapshots** store product name, variant name, SKU, and prices so history survives catalog changes.
5. **`InventoryItem`** maps to table `Inventory` to match the logical schema while avoiding a class name clash.
6. **Check constraints** enforce non-negative prices/quantities at the database level for money and stock fields.

## Next milestones (not in this stage)

- Product / category / order CRUD services and thin MVC controllers
- Inventory movements and reservation workflow
- CSV import pipeline
- PDF order confirmation metadata generation
- Reporting dashboards
