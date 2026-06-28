namespace SalahBahazad.Application.Features.App.DTOs;

/// <summary>
/// Response for <c>GET /api/app/version-status</c> (contract §F.1, FR-APP-UPD-001).
/// The app uses <see cref="Status"/> to decide whether to block, nudge, or proceed silently.
/// </summary>
public sealed record AppVersionStatusDto(
    /// <summary><c>ok</c> | <c>update_available</c> | <c>update_required</c></summary>
    string Status,
    string MinVersion,
    string LatestVersion,
    /// <summary>Store / download URL for this platform; empty string when not yet configured.</summary>
    string StoreUrl);
