using Mediator;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetAssignmentBehaviour;

/// <summary>
/// The in-assessment behaviour timeline for an enrollment's assignment (contract §C #8, FR-ADM-REV-003): the
/// ordered entered/left/answered/navigated events from <c>assessment_events</c>. Staff-only. 404 when the
/// enrollment has no assignment in the caller's tenant.
/// </summary>
public sealed record GetAssignmentBehaviourQuery(Guid EnrollmentId) : IRequest<IReadOnlyList<BehaviourEventDto>>;
