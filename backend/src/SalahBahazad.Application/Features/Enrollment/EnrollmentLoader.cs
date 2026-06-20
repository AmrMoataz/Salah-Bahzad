using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment;

/// <summary>
/// Loads a single <see cref="EnrollmentDto"/> with its display joins (student name, session title, code
/// serial). Shared by the unlock/refund/redeem handlers so all three return the same fully-populated shape.
/// Names ignore query filters so an archived session/student still resolves; the ids come from the
/// tenant-scoped enrollment.
/// </summary>
internal static class EnrollmentLoader
{
    public static async Task<EnrollmentDto?> LoadDtoAsync(
        IAppDbContext db, Guid enrollmentId, CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment is null)
            return null;

        var studentName = await db.Students
            .IgnoreQueryFilters()
            .Where(s => s.Id == enrollment.StudentId)
            .Select(s => s.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var sessionTitle = await db.Sessions
            .IgnoreQueryFilters()
            .Where(s => s.Id == enrollment.SessionId)
            .Select(s => s.Title)
            .FirstOrDefaultAsync(cancellationToken);

        string? codeSerial = null;
        if (enrollment.CodeId is Guid codeId)
            codeSerial = await db.Codes
                .IgnoreQueryFilters()
                .Where(c => c.Id == codeId)
                .Select(c => c.Serial)
                .FirstOrDefaultAsync(cancellationToken);

        return enrollment.ToDto(studentName, sessionTitle, codeSerial);
    }
}
