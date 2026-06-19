using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.UpdateSpecialization;

internal sealed class UpdateSpecializationHandler(IAppDbContext db)
    : IRequestHandler<UpdateSpecializationCommand, SpecializationDto>
{
    public async ValueTask<SpecializationDto> Handle(
        UpdateSpecializationCommand command, CancellationToken cancellationToken)
    {
        var specialization = await db.Specializations.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Specialization", command.Id);

        // The target subject must exist (and be live) in the caller's tenant (FR-PLAT-TAX-002).
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == command.SubjectId, cancellationToken)
            ?? throw new NotFoundException("Subject", command.SubjectId);

        var name = command.Name.Trim();
        if (await db.Specializations.AnyAsync(
                sp => sp.Id != command.Id
                      && sp.SubjectId == subject.Id
                      && sp.Name.ToLower() == name.ToLower(),
                cancellationToken))
            throw new ConflictException($"A specialization named '{name}' already exists under this subject.");

        specialization.Update(name, subject.Id);
        await db.SaveChangesAsync(cancellationToken);

        return specialization.ToDto(subject.Name);
    }
}
