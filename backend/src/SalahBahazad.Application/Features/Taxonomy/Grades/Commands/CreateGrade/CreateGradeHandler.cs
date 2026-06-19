using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Taxonomy.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Taxonomy.Grades.Commands.CreateGrade;

internal sealed class CreateGradeHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    ILogger<CreateGradeHandler> logger)
    : IRequestHandler<CreateGradeCommand, GradeDto>
{
    public async ValueTask<GradeDto> Handle(CreateGradeCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();

        // Friendly duplicate check among live rows; the filtered-unique index is the backstop.
        if (await db.Grades.AnyAsync(g => g.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A grade named '{name}' already exists.");

        var grade = Grade.Create(currentUser.TenantId, name);
        db.Grades.Add(grade);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Grade {GradeId} created by {ActorId}", grade.Id, currentUser.UserId);
        return grade.ToDto();
    }
}
