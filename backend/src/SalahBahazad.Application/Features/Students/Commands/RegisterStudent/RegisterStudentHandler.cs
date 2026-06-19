using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Students.Commands.RegisterStudent;

/// <summary>
/// Verifies the Firebase identity, resolves the tenant by slug, validates the referenced grade/city/
/// region, uploads the ID image to the private bucket, then creates a Pending student and writes an
/// explicit registration audit entry. Runs anonymously, so there is no JWT tenant claim — the audit is
/// written via <see cref="IAuditWriter"/> with the resolved tenant (the interceptor is a no-op here).
/// </summary>
internal sealed class RegisterStudentHandler(
    IAppDbContext db,
    IFirebaseAuthService firebaseAuth,
    IFileStorage fileStorage,
    IAuditWriter auditWriter,
    TimeProvider clock)
    : IRequestHandler<RegisterStudentCommand, StudentRegistrationResultDto>
{
    public async ValueTask<StudentRegistrationResultDto> Handle(
        RegisterStudentCommand command, CancellationToken cancellationToken)
    {
        var claims = await firebaseAuth.VerifyIdTokenAsync(command.FirebaseIdToken, cancellationToken);

        // Tenant is the root (no query filter); the student portal supplies its tenant slug.
        var slug = command.TenantSlug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken)
            ?? throw new NotFoundException("Tenant", command.TenantSlug);

        // Anonymous: no tenant claim, so the global filters resolve to Guid.Empty — query with explicit
        // tenant scope and ignore filters for the tenant-owned lookups.
        var gradeExists = await db.Grades
            .IgnoreQueryFilters()
            .AnyAsync(g => g.Id == command.GradeId && g.TenantId == tenant.Id && !g.IsDeleted, cancellationToken);
        if (!gradeExists)
            throw new NotFoundException("Grade", command.GradeId);

        var cityExists = await db.Cities.AnyAsync(c => c.Id == command.CityId, cancellationToken);
        if (!cityExists)
            throw new NotFoundException("City", command.CityId);

        var regionExists = await db.Regions
            .AnyAsync(r => r.Id == command.RegionId && r.CityId == command.CityId, cancellationToken);
        if (!regionExists)
            throw new NotFoundException("Region", command.RegionId);

        var alreadyRegistered = await db.Students
            .IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == tenant.Id && s.FirebaseUid == claims.Uid, cancellationToken);
        if (alreadyRegistered)
            throw new ConflictException("An account already exists for this sign-in.");

        // Upload first so the key is only persisted when the bytes are safely stored. A failed commit
        // afterwards leaves an orphaned object (cheap to GC), never a dangling key.
        var objectKey = BuildObjectKey(tenant.Id, command.IdImageContentType);
        await fileStorage.UploadPrivateAsync(
            objectKey, command.IdImageContent, command.IdImageContentType, cancellationToken);

        var student = Student.Register(
            tenant.Id,
            claims.Uid,
            command.FullName,
            command.ParentPhonePrimary,
            command.ParentPhoneSecondary,
            command.GradeId,
            command.CityId,
            command.RegionId,
            command.SchoolName,
            command.TermsVersion,
            clock.GetUtcNow());
        student.AttachIdImage(objectKey);

        db.Students.Add(student);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "StudentRegistered",
                EntityType: "Student",
                EntityId: student.Id,
                Summary: "Student self-registered (pending review).",
                TenantId: tenant.Id,
                ActorType: "Student",
                Portal: "student"),
            cancellationToken);

        return new StudentRegistrationResultDto(student.Id, student.Status);
    }

    private static string BuildObjectKey(Guid tenantId, string contentType)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin",
        };
        return $"students/id-images/{tenantId}/{Guid.CreateVersion7():n}{extension}";
    }
}
