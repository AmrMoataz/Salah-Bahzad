using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.UpdateSubject;

internal sealed class UpdateSubjectHandler(IAppDbContext db)
    : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    public async ValueTask<SubjectDto> Handle(UpdateSubjectCommand command, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Subject", command.Id);

        var name = command.Name.Trim();
        if (await db.Subjects.AnyAsync(s => s.Id != command.Id && s.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A subject named '{name}' already exists.");

        subject.Rename(name);
        await db.SaveChangesAsync(cancellationToken);

        var specializationCount = await db.Specializations.CountAsync(sp => sp.SubjectId == subject.Id, cancellationToken);
        return subject.ToDto(specializationCount);
    }
}
