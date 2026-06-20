using Mediator;
using SalahBahazad.Application.Features.Dashboard.DTOs;

namespace SalahBahazad.Application.Features.Dashboard.Queries.GetDashboard;

/// <summary>
/// The dashboard read (contract #2, scrDashboard): tenant-scoped KPIs + the enrollments series + recent activity
/// over a period (<c>7d</c>|<c>30d</c>|<c>90d</c>, default <c>30d</c>) or an explicit <c>From</c>/<c>To</c> range.
/// </summary>
public sealed record GetDashboardQuery(
    string? Period = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IRequest<DashboardDto>;
