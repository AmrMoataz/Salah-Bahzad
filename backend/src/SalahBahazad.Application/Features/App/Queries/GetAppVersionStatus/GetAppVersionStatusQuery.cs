using Mediator;
using SalahBahazad.Application.Features.App.DTOs;

namespace SalahBahazad.Application.Features.App.Queries.GetAppVersionStatus;

/// <param name="Platform">Canonical platform name: <c>android | ios | windows | macos</c> (case-insensitive).</param>
/// <param name="Version">The calling app's semantic version string, e.g. <c>1.0.0</c>.</param>
public sealed record GetAppVersionStatusQuery(string Platform, string Version)
    : IRequest<AppVersionStatusDto>;
