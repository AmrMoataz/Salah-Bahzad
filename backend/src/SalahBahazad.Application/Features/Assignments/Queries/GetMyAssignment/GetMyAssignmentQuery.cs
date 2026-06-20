using Mediator;
using SalahBahazad.Application.Features.Assignments.DTOs;

namespace SalahBahazad.Application.Features.Assignments.Queries.GetMyAssignment;

/// <summary>
/// The calling student's assignment for a session (contract §A #1, FR-PLAT-ASG-002) — the snapshot the
/// enrol side-effect generated. Resumable: returns the saved answers and accumulated time. The student is read
/// from the JWT; 404 when the caller has no assignment for the session.
/// </summary>
public sealed record GetMyAssignmentQuery(Guid SessionId) : IRequest<StudentAssignmentDto>;
