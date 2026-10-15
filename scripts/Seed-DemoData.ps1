<#
.SYNOPSIS
    Applies migrations and seeds a rich demo dataset into TechStore_Dev (idempotent).

.DESCRIPTION
    Runs: dotnet run --project src/TechStore.Web -- --seed-demo
    Marker warehouse code WH-MAIN — if it already exists, seeding is skipped.

.EXAMPLE
    .\scripts\Seed-DemoData.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Seeding demo data (Development / LocalDB TechStore_Dev)…" -ForegroundColor Cyan
dotnet run --project src/TechStore.Web --no-launch-profile -- --seed-demo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Seed failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host @"

Demo accounts (password for all: ChangeMe123!):
  admin@techstore.local      Admin
  manager@techstore.local    Manager
  manager2@techstore.local   Manager
  customer1@techstore.local  Customer
  customer2@techstore.local  Customer

Includes: categories/brands/products/variants, warehouses+inventory,
suppliers+POs, customers+orders/payments/shipments, returns, stock counts,
settings, import batch, audit logs.

Re-run is safe (skips if warehouse WH-MAIN already exists).
"@ -ForegroundColor Green
