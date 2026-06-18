using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Staff.Queries.GetStaffById;

internal sealed class GetStaffByIdHandler(IAppDbContext db)
    : IRequestHandler<GetStaffByIdQuery, StaffDto>
{
    public async ValueTask<StaffDto> Handle(GetStaffByIdQuery query, CancellationToken cancellationToken)
    {
        var staff = await db.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException("Staff member", query.Id);

        return staff.ToDto();
    }
}
