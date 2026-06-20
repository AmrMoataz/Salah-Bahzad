using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Commands.RedeemCode;

/// <summary>
/// A student redeems a code for its session (#12, FR-PLAT-ENR-001) — the student-portal path, backend-only
/// this phase. Validates the code is active, value-matches the session price, and the student has no other
/// active enrollment, then enrolls and marks the code used. The student and tenant come from the JWT.
/// </summary>
public sealed record RedeemCodeCommand(string Serial) : IRequest<EnrollmentDto>, ITransactionalRequest;
