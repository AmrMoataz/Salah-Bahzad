using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Queries.GetSessionById;

internal sealed class GetSessionByIdHandler(IAppDbContext db, IFileStorage fileStorage)
    : IRequestHandler<GetSessionByIdQuery, SessionDetailDto>
{
    public async ValueTask<SessionDetailDto> Handle(GetSessionByIdQuery query, CancellationToken cancellationToken)
        => await SessionDetailLoader.LoadAsync(db, fileStorage, query.Id, cancellationToken)
           ?? throw new NotFoundException("Session", query.Id);
}
