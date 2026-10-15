namespace TechStore.Application.Features.Settings;

public record SettingDto(string Key, string? Value, string? Description, DateTime UpdatedAt);

public class UpsertSettingRequest
{
    public string? Value { get; set; }
    public string? Description { get; set; }
}

public static class AllowedSettings
{
    public const string StoreDisplayName = "StoreDisplayName";
    public const string DefaultLowStockThreshold = "DefaultLowStockThreshold";
    public const string CurrencyCode = "CurrencyCode";
    public const string MaxImportFileBytes = "MaxImportFileBytes";
    public const string DocumentIssuerName = "DocumentIssuerName";

    public static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
    {
        StoreDisplayName,
        DefaultLowStockThreshold,
        CurrencyCode,
        MaxImportFileBytes,
        DocumentIssuerName
    };
}
