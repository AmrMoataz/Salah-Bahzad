using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Profile.DTOs;

namespace SalahBahazad.Application.Features.Profile.Commands.UpdateMyStudentProfile;

internal sealed class UpdateMyStudentProfileHandler(IAppDbContext db, ICurrentUserResolver currentUser)
    : IRequestHandler<UpdateMyStudentProfileCommand, StudentProfileDto>
{
    public async ValueTask<StudentProfileDto> Handle(
        UpdateMyStudentProfileCommand command, CancellationToken cancellationToken)
    {
        // The caller can only edit their own record — resolved from the JWT, never a URL id (NFR-SEC-007). The
        // subject always exists (no documented 404-self, §B); the guard is defensive only. Tracked (not AsNoTracking)
        // so the mutation is persisted; the EF global filter scopes it to the caller's tenant.
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Profile", currentUser.UserId);

        // City/region existence + region-belongs-to-city → 400 (§C.3). City/Region are global reference data (no
        // tenant filter). Unlike the anonymous S1 register handler (which 404s on a bad reference), S6 surfaces these
        // as field-level validation failures so the edit form can flag the offending dropdown (§B).
        var cityExists = await db.Cities.AnyAsync(c => c.Id == command.CityId, cancellationToken);
        if (!cityExists)
            throw new ValidationException([new ValidationFailure(
                nameof(command.CityId), "The selected city does not exist.")]);

        var regionBelongsToCity = await db.Regions
            .AnyAsync(r => r.Id == command.RegionId && r.CityId == command.CityId, cancellationToken);
        if (!regionBelongsToCity)
            throw new ValidationException([new ValidationFailure(
                nameof(command.RegionId), "The selected region does not belong to the selected city.")]);

        // Applies exactly the seven writable fields and leaves GradeId unchanged (§C.1). The SaveChanges runs inside
        // the transaction pipeline and is audited by the interceptor as an "Updated Student" row (ActorType=Student).
        student.UpdateOwnProfile(
            command.FullName,
            command.PhoneNumber,
            command.SchoolName,
            command.CityId,
            command.RegionId,
            command.ParentPhonePrimary,
            command.ParentPhoneSecondary);

        await db.SaveChangesAsync(cancellationToken);

        // Re-read through the shared loader so the response carries the freshly resolved grade/city/region names and
        // the active bound device — a consistent, fully-populated record (§A.1).
        return await StudentProfileLoader.LoadAsync(db, student.Id, cancellationToken)
            ?? throw new NotFoundException("Profile", student.Id);
    }
}
