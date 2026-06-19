using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.ReorderSessionVideos;

/// <summary>Reassigns video order from a complete, exactly-matching id list (FR-ADM-SES-003).</summary>
public sealed record ReorderSessionVideosCommand(
    Guid SessionId, IReadOnlyList<Guid> OrderedVideoIds)
    : IRequest<IReadOnlyList<SessionVideoDto>>, ITransactionalRequest;
