using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Commands.EnableCode;

internal sealed class EnableCodeHandler(IAppDbContext db)
    : IRequestHandler<EnableCodeCommand, CodeListDto>
{
    public async ValueTask<CodeListDto> Handle(EnableCodeCommand command, CancellationToken cancellationToken)
    {
        var code = await db.Codes.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Code", command.Id);

        try
        {
            code.Enable();
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
