using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.CreateSubject;

internal sealed class CreateSubjectHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    ILogger<CreateSubjectHandler> logger)
    : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    public async ValueTask<SubjectDto> Handle(CreateSubjectCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();

        if (await db.Subjects.AnyAsync(s => s.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A subject named '{name}' already exists.");

        var subject = Subject.Create(currentUser.TenantId, name);
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Subject {SubjectId} created by {ActorId}", subject.Id, currentUser.UserId);

        // A freshly created subject has no specializations yet.
        return subject.ToDto(specializationCount: 0);
    }
}
