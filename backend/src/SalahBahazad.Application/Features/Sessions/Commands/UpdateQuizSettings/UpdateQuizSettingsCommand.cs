using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateQuizSettings;

/// <summary>
/// Sets/replaces the session's gating-quiz configuration (FR-PLAT-SES-006, FR-ADM-QZ-001). Ranges are
/// validated (→ 400). The <c>questionCount ≤ quizEligibleQuestionCount</c> rule is a client-side warning
/// here and is hard-blocked server-side on publish (FR-ADM-QZ-002, per the frozen contract).
/// </summary>
public sealed record UpdateQuizSettingsCommand(
    Guid Id,
    int TimeLimitMinutes,
    int QuestionCount,
    int AttemptCount,
    int MinPassPercent) : IRequest<SessionDetailDto>, ITransactionalRequest;
