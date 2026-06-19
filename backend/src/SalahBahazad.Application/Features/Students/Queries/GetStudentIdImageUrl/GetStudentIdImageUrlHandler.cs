using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.GetStudentIdImageUrl;

/// <summary>
/// Looks up the student, writes an explicit access audit entry, then issues a short-lived signed URL.
/// The audit is written <b>before</b> the URL so an ID-image view is never served unlogged
/// (FR-PLAT-AST-003, NFR-PRIV-001/002). The DB only ever holds the object key (FR-PLAT-AST-004).
/// </summary>
internal sealed class GetStudentIdImageUrlHandler(
    IAppDbContext db,
    IFileStorage fileStorage,
    IAuditWriter auditWriter)
    : IRequestHandler<GetStudentIdImageUrlQuery, StudentIdImageUrlDto>
{
    public async ValueTask<StudentIdImageUrlDto> Handle(
        GetStudentIdImageUrlQuery query, CancellationToken cancellationToken)
    {
        var student = await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.StudentId, cancellationToken)
            ?? throw new NotFoundException("Student", query.StudentId);

        if (string.IsNullOrWhiteSpace(student.IdImageObjectKey))
            throw new NotFoundException("ID-verification image for student", query.StudentId);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "StudentIdImageViewed",
                EntityType: "Student",
                EntityId: student.Id,
                Summary: "Viewed student ID-verification image."),
            cancellationToken);

        var signed = await fileStorage.GetSignedReadUrlAsync(
            student.IdImageObjectKey, cancellationToken: cancellationToken);

        return new StudentIdImageUrlDto(signed.Url, signed.ExpiresAtUtc);
    }
}
