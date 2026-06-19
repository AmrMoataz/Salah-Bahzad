using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.RejectStudent;

internal sealed class RejectStudentHandler(IAppDbContext db)
    : IRequestHandler<RejectStudentCommand, StudentDetailDto>
{
    public async ValueTask<StudentDetailDto> Handle(RejectStudentCommand command, CancellationToken cancellationToken)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);

        try
        {
            student.Reject(command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        // The StudentRejectedEvent carries the reason into the audit Summary (FR-ADM-STU-010).
        await db.SaveChangesAsync(cancellationToken);

        return await StudentDetailLoader.LoadAsync(db, student.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);
    }
}
