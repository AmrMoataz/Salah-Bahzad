using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.CreateSpecialization;

internal sealed class CreateSpecializationHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    ILogger<CreateSpecializationHandler> logger)
    : IRequestHandler<CreateSpecializationCommand, SpecializationDto>
{
    public async ValueTask<SpecializationDto> Handle(
        CreateSpecializationCommand command, CancellationToken cancellationToken)
    {
        // The owning subject must exist (and be live) in the caller's tenant (FR-PLAT-TAX-002).
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == command.SubjectId, cancellationToken)
            ?? throw new NotFoundException("Subject", command.SubjectId);

        var name = command.Name.Trim();
        if (await db.Specializations.AnyAsync(
                sp => sp.SubjectId == subject.Id && sp.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A specialization named '{name}' already exists under this subject.");

        var specialization = Specialization.Create(currentUser.TenantId, subject.Id, name);
        db.Specializations.Add(specialization);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Specialization {SpecializationId} created under subject {SubjectId} by {ActorId}",
            specialization.Id, subject.Id, currentUser.UserId);

        return specialization.ToDto(subject.Name);
    }
}
