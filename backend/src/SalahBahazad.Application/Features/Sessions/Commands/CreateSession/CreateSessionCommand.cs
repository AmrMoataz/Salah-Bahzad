using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.CreateSession;

/// <summary>
/// Creates a Draft session with its core authoring details (FR-PLAT-SES-001, FR-ADM-SES-002). The
/// referenced grade and specialization must exist in the caller's tenant. Transactional so the row and its
/// creation audit entry commit together.
/// </summary>
public sealed record CreateSessionCommand(
    string Title,
    string? Description,
    decimal Price,
    int ValidityDays,
    Guid GradeId,
    Guid SpecializationId) : IRequest<SessionDetailDto>, ITransactionalRequest;
