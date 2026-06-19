using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.SetStudentActive;

internal sealed class SetStudentActiveHandler(IAppDbContext db, IFirebaseAuthService firebaseAuth)
    : IRequestHandler<SetStudentActiveCommand, StudentDetailDto>
{
    public async ValueTask<StudentDetailDto> Handle(SetStudentActiveCommand command, CancellationToken cancellationToken)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);

        try
        {
            if (command.IsActive)
                student.Reactivate();
            else
                student.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        // DB Status is the authoritative gate (sign-in is refused when inactive); mirror to Firebase so
        // a deactivated student cannot mint fresh ID tokens (mirrors SetStaffActiveHandler).
        await db.SaveChangesAsync(cancellationToken);
        await firebaseAuth.SetUserDisabledAsync(student.FirebaseUid, !command.IsActive, cancellationToken);

        return await StudentDetailLoader.LoadAsync(db, student.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);
    }
}
