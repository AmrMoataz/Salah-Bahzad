using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Sessions.Commands.RemoveSessionVideo;

/// <summary>Removes a video from a session (FR-ADM-SES-003).</summary>
public sealed record RemoveSessionVideoCommand(Guid SessionId, Guid VideoId)
    : IRequest<Unit>, ITransactionalRequest;
