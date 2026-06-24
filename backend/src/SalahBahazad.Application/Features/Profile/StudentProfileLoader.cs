using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Profile.DTOs;

namespace SalahBahazad.Application.Features.Profile;

/// <summary>
/// Loads the signed-in student's own <see cref="StudentProfileDto"/>, resolving the display names for the (tenant)
/// grade and (global) city/region plus the caller's active bound device — the shape behind both the GET read and
/// the PUT re-read so they return a consistent record (mirrors <c>StudentDetailLoader</c>, Student-Portal S6 §C.3).
/// The student is scoped by the EF global tenant filter (caller's tenant + not soft-deleted); grade lookups ignore
/// query filters so an archived grade still shows its name. The raw device-token hash is never projected (§C.5).
/// Returns null when no student matches in the caller's tenant — for <c>/api/me/profile</c> the subject is the JWT
/// principal and so always exists (there is no documented 404-self, §B); the null guard is defensive only.
/// </summary>
internal static class StudentProfileLoader
{
    public static async Task<StudentProfileDto?> LoadAsync(
        IAppDbContext db, Guid studentId, CancellationToken cancellationToken)
    {
        var student = await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
            return null;

        var gradeName = await db.Grades
            .IgnoreQueryFilters()
            .Where(g => g.Id == student.GradeId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var cityName = await db.Cities
            .Where(c => c.Id == student.CityId)
            .Select(c => c.NameEn)
            .FirstOrDefaultAsync(cancellationToken);

        var regionName = await db.Regions
            .Where(r => r.Id == student.RegionId)
            .Select(r => r.NameEn)
            .FirstOrDefaultAsync(cancellationToken);

        var activeDevice = await db.StudentDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StudentId == student.Id && d.IsActive, cancellationToken);

        return student.ToProfileDto(gradeName, cityName, regionName, activeDevice);
    }
}
