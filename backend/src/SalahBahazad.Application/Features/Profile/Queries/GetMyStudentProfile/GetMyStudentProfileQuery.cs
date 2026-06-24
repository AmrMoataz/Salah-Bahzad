using Mediator;
using SalahBahazad.Application.Features.Profile.DTOs;

namespace SalahBahazad.Application.Features.Profile.Queries.GetMyStudentProfile;

/// <summary>
/// Returns the signed-in student's own profile (Student-Portal S6 §A #1, FR-STU-PRO-001/002). The student + tenant
/// come from the JWT (never a URL id) — a pure read of the caller's own data, so it is <b>not</b> audited (§E).
/// </summary>
public sealed record GetMyStudentProfileQuery : IRequest<StudentProfileDto>;
