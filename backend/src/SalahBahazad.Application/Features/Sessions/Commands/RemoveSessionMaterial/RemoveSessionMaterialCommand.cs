using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Sessions.Commands.RemoveSessionMaterial;

/// <summary>Removes a material from a session (FR-ADM-SES-004).</summary>
public sealed record RemoveSessionMaterialCommand(Guid SessionId, Guid MaterialId)
    : IRequest<Unit>, ITransactionalRequest;
