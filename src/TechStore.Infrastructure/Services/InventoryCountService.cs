using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.InventoryCounts;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class InventoryCountService : IInventoryCountService
{
    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public InventoryCountService(ApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<Result<InventoryCountSessionDto>> CreateAsync(
        CreateInventoryCountRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
        if (warehouse is null || !warehouse.IsActive)
        {
            return Result.Failure<InventoryCountSessionDto>("Warehouse not found or inactive.", ResultCodes.NotFound);
        }

        var items = await _db.Inventory
            .Where(i => i.WarehouseId == request.WarehouseId)
            .Include(i => i.Variant)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var session = new InventoryCountSession
        {
            SessionNumber = $"CNT-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..28],
            WarehouseId = warehouse.Id,
            Status = InventoryCountStatus.InProgress,
            CreatedByUserId = userId,
            SnapshotTakenAt = now,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var item in items)
        {
            session.Lines.Add(new InventoryCountLine
            {
                VariantId = item.VariantId,
                InventoryItemId = item.Id,
                ExpectedQuantity = item.QuantityOnHand,
                ExpectedRowVersion = item.RowVersion.ToArray()
            });
        }

        _db.InventoryCountSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success((await LoadAsync(session.Id, cancellationToken))!);
    }

    public Task<InventoryCountSessionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => LoadAsync(id, cancellationToken);

    public async Task<Result<InventoryCountSessionDto>> SetActualsAsync(
        int id,
        SetCountActualsRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.InventoryCountSessions.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return Result.NotFound<InventoryCountSessionDto>("Count session not found.");
        }

        if (session.Status != InventoryCountStatus.InProgress)
        {
            return Result.Conflict<InventoryCountSessionDto>(
                "Actuals can only be set while the session is InProgress.",
                ResultCodes.InvalidTransition);
        }

        foreach (var lineReq in request.Lines)
        {
            if (lineReq.ActualQuantity < 0)
            {
                return Result.Failure<InventoryCountSessionDto>("Actual quantity cannot be negative.");
            }

            var line = session.Lines.FirstOrDefault(l => l.Id == lineReq.LineId);
            if (line is null)
            {
                return Result.Failure<InventoryCountSessionDto>($"Line {lineReq.LineId} not found in session.");
            }

            line.ActualQuantity = lineReq.ActualQuantity;
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success((await LoadAsync(session.Id, cancellationToken))!);
    }

    public async Task<Result<InventoryCountSessionDto>> SubmitForReviewAsync(int id, CancellationToken cancellationToken = default)
    {
        var session = await _db.InventoryCountSessions.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return Result.NotFound<InventoryCountSessionDto>("Count session not found.");
        }

        if (session.Status != InventoryCountStatus.InProgress)
        {
            return Result.Conflict<InventoryCountSessionDto>(
                "Only InProgress sessions can be submitted for review.",
                ResultCodes.InvalidTransition);
        }

        if (session.Lines.Any(l => !l.ActualQuantity.HasValue))
        {
            return Result.Failure<InventoryCountSessionDto>("All count lines must have an actual quantity before review.");
        }

        session.Status = InventoryCountStatus.PendingReview;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success((await LoadAsync(session.Id, cancellationToken))!);
    }

    public async Task<Result<InventoryCountSessionDto>> ApplyAsync(
        int id,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var session = await _db.InventoryCountSessions.Include(s => s.Lines)
                    .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
                if (session is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<InventoryCountSessionDto>("Count session not found.");
                }

                if (session.AdjustmentsApplied || session.Status == InventoryCountStatus.Applied)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Success((await LoadAsync(session.Id, cancellationToken))!);
                }

                if (session.Status != InventoryCountStatus.PendingReview)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<InventoryCountSessionDto>(
                        "Only PendingReview sessions can be applied.",
                        ResultCodes.InvalidTransition);
                }

                foreach (var line in session.Lines)
                {
                    if (!line.ActualQuantity.HasValue)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<InventoryCountSessionDto>("All lines must have actual quantities.");
                    }

                    var apply = await _inventory.ApplyCountAdjustmentInAmbientAsync(
                        line.InventoryItemId,
                        line.ExpectedRowVersion,
                        line.ExpectedQuantity,
                        line.ActualQuantity.Value,
                        session.Id,
                        userId,
                        cancellationToken);
                    if (apply.IsFailure)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<InventoryCountSessionDto>(
                            apply.Error!,
                            apply.Code ?? ResultCodes.ConcurrencyConflict);
                    }

                    line.AppliedMovementId = apply.Value;
                }

                session.AdjustmentsApplied = true;
                session.Status = InventoryCountStatus.Applied;
                session.CompletedAt = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result.Success((await LoadAsync(session.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<InventoryCountSessionDto>> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var session = await _db.InventoryCountSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return Result.NotFound<InventoryCountSessionDto>("Count session not found.");
        }

        if (session.Status is InventoryCountStatus.Applied or InventoryCountStatus.Cancelled)
        {
            return Result.Conflict<InventoryCountSessionDto>(
                "Applied or cancelled sessions cannot be cancelled.",
                ResultCodes.InvalidTransition);
        }

        session.Status = InventoryCountStatus.Cancelled;
        session.CancelledAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success((await LoadAsync(session.Id, cancellationToken))!);
    }

    private async Task<InventoryCountSessionDto?> LoadAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.InventoryCountSessions.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new InventoryCountSessionDto(
                s.Id,
                s.SessionNumber,
                s.WarehouseId,
                s.Warehouse.Name,
                s.Status,
                s.SnapshotTakenAt,
                s.CompletedAt,
                s.CancelledAt,
                s.AdjustmentsApplied,
                s.Notes,
                s.Lines.OrderBy(l => l.Id).Select(l => new InventoryCountLineDto(
                    l.Id,
                    l.VariantId,
                    l.Variant.Sku,
                    l.InventoryItemId,
                    l.ExpectedQuantity,
                    l.ActualQuantity,
                    l.ActualQuantity.HasValue ? l.ActualQuantity.Value - l.ExpectedQuantity : null,
                    l.AppliedMovementId)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
