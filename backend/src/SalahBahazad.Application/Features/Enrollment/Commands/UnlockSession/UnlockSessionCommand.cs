using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Commands.UnlockSession;

/// <summary>
/// Staff grant a student direct access to a session, bypassing code &amp; price (#9, FR-PLAT-ENR-002).
/// Create-or-extend through the shared enrollment path. Transactional so enrollment + counters + payment +
/// attendance shell commit together and the enrollment event dispatches post-commit.
/// </summary>
public sealed record UnlockSessionCommand(Guid SessionId, Guid StudentId)
    : IRequest<EnrollmentDto>, ITransactionalRequest;
