using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.UpdateStudentContact;

internal sealed class UpdateStudentContactHandler(IAppDbContext db)
    : IRequestHandler<UpdateStudentContactCommand, StudentDetailDto>
{
    public async ValueTask<StudentDetailDto> Handle(
        UpdateStudentContactCommand command, CancellationToken cancellationToken)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);

        // The grade must exist in the caller's tenant (the global filter scopes this to the tenant).
        var gradeExists = await db.Grades.AnyAsync(g => g.Id == command.GradeId, cancellationToken);
        if (!gradeExists)
            throw new NotFoundException("Grade", command.GradeId);

        student.UpdateContactInfo(command.GradeId, command.ParentPhonePrimary, command.ParentPhoneSecondary);
        await db.SaveChangesAsync(cancellationToken);

        return await StudentDetailLoader.LoadAsync(db, student.Id, cancellationToken)
            ?? throw new NotFoundException("Student", command.Id);
    }
}
