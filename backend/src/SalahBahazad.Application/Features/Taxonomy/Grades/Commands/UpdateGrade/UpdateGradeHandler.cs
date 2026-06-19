using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.UpdateGrade;

internal sealed class UpdateGradeHandler(IAppDbContext db)
    : IRequestHandler<UpdateGradeCommand, GradeDto>
{
    public async ValueTask<GradeDto> Handle(UpdateGradeCommand command, CancellationToken cancellationToken)
    {
        var grade = await db.Grades.FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Grade", command.Id);

        var name = command.Name.Trim();
        if (await db.Grades.AnyAsync(g => g.Id != command.Id && g.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A grade named '{name}' already exists.");

        grade.Rename(name);
        await db.SaveChangesAsync(cancellationToken);

        return grade.ToDto();
    }
}
