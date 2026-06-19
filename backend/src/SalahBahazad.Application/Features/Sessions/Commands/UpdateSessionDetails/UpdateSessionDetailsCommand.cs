using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionDetails;

/// <summary>Updates a session's core authoring details (FR-ADM-SES-002).</summary>
public sealed record UpdateSessionDetailsCommand(
    Guid Id,
    string Title,
    string? Description,
    decimal Price,
    int ValidityDays,
    Guid GradeId,
    Guid SpecializationId) : IRequest<SessionDetailDto>, ITransactionalRequest;
