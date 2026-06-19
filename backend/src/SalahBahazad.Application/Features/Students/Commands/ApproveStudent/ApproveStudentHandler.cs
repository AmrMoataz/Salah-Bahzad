using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.ApproveStudent;

internal sealed class ApproveStudentHandler(IAppDbContext db)
    : IRequestHandler<ApproveStudentCommand, StudentDetailDto>
{
    public async ValueTask<StudentDetailDto> Handle(ApproveStudentCommand command, CancellationToken cancellationToken)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);

        // The domain enforces the legal transition; surface an illegal one as 409 rather than 500.
        try
        {
            student.Approve();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        // The StudentApprovedEvent enriches this save's audit entry (FR-ADM-STU-010).
        await db.SaveChangesAsync(cancellationToken);

        return await StudentDetailLoader.LoadAsync(db, student.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);
    }
}
