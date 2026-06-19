using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Sessions.Commands.DeleteSession;

/// <summary>Soft-deletes a session so enrollment/history survive (FR-ADM-SES-011, FR-PLAT-ROLE-004).</summary>
public sealed record DeleteSessionCommand(Guid Id) : IRequest<Unit>, ITransactionalRequest;
