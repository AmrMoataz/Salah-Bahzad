using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Assignments.DTOs;

namespace SalahBahazad.Application.Features.Assignments.Commands.AnswerQuestion;

/// <summary>
/// Records the calling student's answer to one assignment question (contract §A #2, FR-PLAT-ASG-003) and, when
/// that completes the assignment, auto-grades it (FR-PLAT-ASG-006). Transactional so the answer, the behaviour
/// event and (on completion) the grade event commit together; the grade event then drives the attendance write.
/// </summary>
public sealed record AnswerQuestionCommand(Guid AssignmentId, Guid AssignmentQuestionId, Guid SelectedOptionId)
    : IRequest<AssignmentProgressDto>, ITransactionalRequest;
