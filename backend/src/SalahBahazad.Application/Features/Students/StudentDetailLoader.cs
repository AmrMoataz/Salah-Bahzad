using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students;

/// <summary>
/// Loads the 360° <see cref="StudentDetailDto"/> for a student, resolving the display names for the
/// (tenant) grade and (global) city/region plus the current active device. Shared by the detail query
/// and every mutation handler so they return a consistent, fully-populated record. Grade lookups
/// ignore query filters so a student whose grade was later archived still shows its name (the grade is
/// soft-deleted, FR-PLAT-ROLE-004). Returns null when no student matches in the caller's tenant.
/// </summary>
internal static class StudentDetailLoader
{
    public static async Task<StudentDetailDto?> LoadAsync(
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

        return student.ToDetailDto(gradeName, cityName, regionName, activeDevice);
    }
}
