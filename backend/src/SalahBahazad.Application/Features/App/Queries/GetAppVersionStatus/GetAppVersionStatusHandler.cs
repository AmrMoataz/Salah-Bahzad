using Mediator;
using Microsoft.Extensions.Options;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Features.App.DTOs;

namespace SalahBahazad.Application.Features.App.Queries.GetAppVersionStatus;

/// <summary>
/// Returns whether the calling app version is current, updatable, or below the enforced minimum floor
/// (contract §F.1, FR-APP-UPD-001, NFR-APP-UPD-002). The validator (<see cref="GetAppVersionStatusValidator"/>)
/// runs first via the pipeline — by the time this handler executes Platform and Version are already validated.
/// Reads from <see cref="AppVersionsOptions"/> which is hot-reloadable — no redeploy needed to raise the floor.
/// </summary>
internal sealed class GetAppVersionStatusHandler(IOptionsMonitor<AppVersionsOptions> opts)
    : IRequestHandler<GetAppVersionStatusQuery, AppVersionStatusDto>
{
    public ValueTask<AppVersionStatusDto> Handle(GetAppVersionStatusQuery query, CancellationToken ct)
    {
        var platform = query.Platform.Trim().ToLowerInvariant();

        // Validator guarantees the platform is known and the version is parseable; defensive defaults here.
        _ = opts.CurrentValue.Platforms.TryGetValue(platform, out var pOpts);
        pOpts ??= new PlatformVersionOptions();

        _ = Version.TryParse(query.Version, out var requested);
        requested ??= new Version(0, 0, 0);

        _ = Version.TryParse(pOpts.MinVersion, out var min);
        min ??= new Version(1, 0, 0);

        _ = Version.TryParse(pOpts.LatestVersion, out var latest);
        latest ??= min;

        var status = requested < min ? "update_required"
                   : requested < latest ? "update_available"
                   : "ok";

        return ValueTask.FromResult(new AppVersionStatusDto(status, pOpts.MinVersion, pOpts.LatestVersion, pOpts.StoreUrl));
    }
}
