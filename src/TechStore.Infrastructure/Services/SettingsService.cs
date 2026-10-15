using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Settings;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _db;

    public SettingsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _db.ApplicationSettings.AsNoTracking()
            .Where(s => AllowedSettings.Keys.Contains(s.SettingKey))
            .OrderBy(s => s.SettingKey)
            .Select(s => new SettingDto(s.SettingKey, s.SettingValue, s.Description, s.UpdatedAt))
            .ToListAsync(cancellationToken);

        return AllowedSettings.Keys
            .OrderBy(k => k)
            .Select(k => stored.FirstOrDefault(s => s.Key == k) ?? new SettingDto(k, null, null, default))
            .ToList();
    }

    public async Task<SettingDto?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!AllowedSettings.Keys.Contains(key))
        {
            return null;
        }

        var row = await _db.ApplicationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
        return row is null
            ? new SettingDto(key, null, null, default)
            : new SettingDto(row.SettingKey, row.SettingValue, row.Description, row.UpdatedAt);
    }

    public async Task<Result<SettingDto>> UpsertAsync(
        string key,
        UpsertSettingRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedSettings.Keys.Contains(key))
        {
            return Result.Failure<SettingDto>("Setting key is not allowed.");
        }

        var validation = Validate(key, request.Value);
        if (validation is not null)
        {
            return Result.Failure<SettingDto>(validation);
        }

        var row = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
        var now = DateTime.UtcNow;
        if (row is null)
        {
            row = new ApplicationSetting
            {
                SettingKey = key,
                SettingValue = request.Value,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                UpdatedBy = userId,
                UpdatedAt = now
            };
            _db.ApplicationSettings.Add(row);
        }
        else
        {
            row.SettingValue = request.Value;
            row.Description = string.IsNullOrWhiteSpace(request.Description) ? row.Description : request.Description.Trim();
            row.UpdatedBy = userId;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(new SettingDto(row.SettingKey, row.SettingValue, row.Description, row.UpdatedAt));
    }

    private static string? Validate(string key, string? value)
    {
        switch (key)
        {
            case AllowedSettings.StoreDisplayName:
            case AllowedSettings.DocumentIssuerName:
                if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200)
                {
                    return "Value is required and must be at most 200 characters.";
                }

                break;
            case AllowedSettings.CurrencyCode:
                if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 3 || !value.All(char.IsLetter))
                {
                    return "CurrencyCode must be a 3-letter code.";
                }

                break;
            case AllowedSettings.DefaultLowStockThreshold:
                if (!int.TryParse(value, out var threshold) || threshold < 0)
                {
                    return "DefaultLowStockThreshold must be an integer >= 0.";
                }

                break;
            case AllowedSettings.MaxImportFileBytes:
                if (!int.TryParse(value, out var bytes) || bytes < 1024 || bytes > 5 * 1024 * 1024)
                {
                    return "MaxImportFileBytes must be between 1024 and 5242880.";
                }

                break;
        }

        return null;
    }
}
