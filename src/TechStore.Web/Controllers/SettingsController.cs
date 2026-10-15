using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Settings;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Route("api/settings")]
public class SettingsController : ApiControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings)
    {
        _settings = settings;
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _settings.GetAllAsync(cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
    {
        var item = await _settings.GetAsync(key, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpPut("{key}")]
    public async Task<IActionResult> Upsert(string key, [FromBody] UpsertSettingRequest request, CancellationToken cancellationToken)
        => FromResult(await _settings.UpsertAsync(key, request, CurrentUserId, cancellationToken));
}
