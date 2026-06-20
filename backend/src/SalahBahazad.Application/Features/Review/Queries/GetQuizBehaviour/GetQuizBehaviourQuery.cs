using Mediator;
using SalahBahazad.Application.Features.Review.DTOs;

namespace SalahBahazad.Application.Features.Review.Queries.GetQuizBehaviour;

/// <summary>
/// The focus-loss/return behaviour timeline across a quiz's attempts (contract §B, scrReview Behaviour tab,
/// FR-PLAT-QZ-006). Staff-only; tenant-scoped by the global filter. 404 when the enrollment has no quiz.
/// </summary>
public sealed record GetQuizBehaviourQuery(Guid EnrollmentId) : IRequest<IReadOnlyList<BehaviourEventDto>>;
