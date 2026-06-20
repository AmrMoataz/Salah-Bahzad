using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Commands.DisableCode;

internal sealed class DisableCodeHandler(IAppDbContext db)
    : IRequestHandler<DisableCodeCommand, CodeListDto>
{
    public async ValueTask<CodeListDto> Handle(DisableCodeCommand command, CancellationToken cancellationToken)
    {
        var code = await db.Codes.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Code", command.Id);

        // The domain blocks disabling a used code; surface it as 409 rather than 500.
        try
        {
            code.Disable();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);

        var dtos = await CodeListProjector.ToListDtosAsync(db, [code], cancellationToken);
        return dtos[0];
    }
}
