using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.SetPrerequisite;

/// <summary>
/// Sets or clears a session's prerequisite (FR-ADM-SES-005). A null/empty value clears it. The handler
/// rejects a self-reference or any chain that would form a cycle (409).
/// </summary>
public sealed record SetPrerequisiteCommand(
    Guid Id, Guid? PrerequisiteSessionId) : IRequest<SessionDetailDto>, ITransactionalRequest;
