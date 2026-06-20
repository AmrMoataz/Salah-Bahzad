using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Codes.Commands.DeleteCode;

internal sealed class DeleteCodeHandler(IAppDbContext db, ICurrentUserResolver currentUser, TimeProvider clock)
    : IRequestHandler<DeleteCodeCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteCodeCommand command, CancellationToken cancellationToken)
    {
        var code = await db.Codes.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Code", command.Id);

        try
        {
            code.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
