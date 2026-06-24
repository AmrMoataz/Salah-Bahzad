using Mediator;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetMyPlan;

/// <summary>
/// The student-portal Home read (contract §A, FR-STU-SES-001 et al.): the caller's derived weekly study plan —
/// KPI roll-up, the focus session (Path A current-frontier), the gate-ordered steps (≤ 7), and the recently-enrolled
/// rail. <b>No parameters</b>; the student + tenant come from the JWT (<see cref="Common.Interfaces.ICurrentUserResolver"/>),
/// never a URL id — no IDOR surface. Always returns a plan (an onboarding plan when the caller has no enrollments,
/// never 404). Served from the Redis-backed HybridCache when warm; computed + cached on a miss (§C).
/// </summary>
public sealed record GetMyPlanQuery : IRequest<MyPlanDto>;
