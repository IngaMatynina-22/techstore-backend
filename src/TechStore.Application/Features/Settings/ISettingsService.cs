using TechStore.Application.Common;

namespace TechStore.Application.Features.Settings;

public interface ISettingsService
{
    Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SettingDto?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<Result<SettingDto>> UpsertAsync(string key, UpsertSettingRequest request, int? userId, CancellationToken cancellationToken = default);
}
