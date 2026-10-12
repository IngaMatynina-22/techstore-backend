using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class ApplicationSetting : BaseEntity
{
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string? Description { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
