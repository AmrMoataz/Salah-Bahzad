using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;
using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Students.Commands.RegisterStudent;

/// <summary>
/// Verifies the Firebase identity, resolves the tenant by slug, validates the referenced grade/city/
/// region, uploads the ID image to the private bucket, then creates a Pending student and writes an
/// explicit registration audit entry. Runs anonymously, so there is no JWT tenant claim — the audit is
/// written via <see cref="IAuditWriter"/> with the resolved tenant (the interceptor is a no-op here).
/// <para>
/// If a <see cref="StudentStatus.Rejected"/> student already exists for this Firebase identity, the same
/// row is re-used and moved back to Pending (audited as <c>StudentResubmitted</c>) instead of returning a
/// 409 — so a rejected registration is never a permanent dead-end for that email (FR-ADM-STU-004 follow-up).
/// A live account (Pending/Active/Inactive) still conflicts.
/// </para>
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

        // A prior registration for this Firebase identity is only a hard conflict while it is still a live
        // account (Pending/Active/Inactive). A *Rejected* one is reused — the student corrects their details
        // and re-submits on the same row + same email, so rejection is never a dead-end (FR-ADM-STU-004
        // follow-up). Tracked (not AnyAsync) so Resubmit can mutate it. IgnoreQueryFilters: anonymous, no
        // tenant claim, and a soft-deleted row should still block re-registration.
        var existing = await db.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenant.Id && s.FirebaseUid == claims.Uid, cancellationToken);

        if (existing is not null && existing.Status != StudentStatus.Rejected)
            throw new ConflictException("An account already exists for this sign-in.");

        var now = clock.GetUtcNow();
        var isResubmission = existing is not null;

        // Either branch yields a tracked student with a stable Id before the upload: a fresh Register adds
        // a new row, a resubmission reuses the rejected one. The id seeds the object key (the bucket groups
        // uploads by owner). Upload before attaching/persisting the key, so the key is only saved once the
        // bytes are stored. A failed commit afterwards leaves an orphaned object (cheap to GC), never a
        // dangling key.
        Student student;
        if (existing is not null)
        {
            student = existing;
            student.Resubmit(
                command.FullName,
                command.PhoneNumber,
                command.ParentPhonePrimary,
                command.ParentPhoneSecondary,
                command.GradeId,
                command.CityId,
                command.RegionId,
                command.SchoolName,
                command.TermsVersion,
                now);
        }
        else
        {
            // Mint the tenant-unique watermark serial (FR-APP-VID-003). Anonymous path → no tenant claim, so read
            // with IgnoreQueryFilters + explicit TenantId; include soft-deleted rows so a serial is never reissued
            // (same rationale as the (TenantId, Serial) unique index). NextUnique seeds from this set to avoid an
            // up-front collision; the index is the hard guarantee.
            var existingSerials = await db.Students
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenant.Id)
                .Select(s => s.Serial)
                .ToHashSetAsync(cancellationToken);
            var serial = StudentSerialGenerator.NextUnique(existingSerials);

            student = Student.Register(
                tenant.Id,
                claims.Uid,
                serial,
                command.FullName,
                command.PhoneNumber,
                command.ParentPhonePrimary,
                command.ParentPhoneSecondary,
                command.GradeId,
                command.CityId,
                command.RegionId,
                command.SchoolName,
                command.TermsVersion,
                now);
            db.Students.Add(student);
        }

        var objectKey = StorageKeys.StudentIdImage(tenant.Id, student.Id, command.IdImageContentType);
        await fileStorage.UploadPrivateAsync(
            objectKey, command.IdImageContent, command.IdImageContentType, cancellationToken);
        student.AttachIdImage(objectKey);

        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: isResubmission ? "StudentResubmitted" : "StudentRegistered",
                EntityType: "Student",
                EntityId: student.Id,
                Summary: isResubmission
                    ? "Rejected student re-submitted registration (pending review)."
                    : "Student self-registered (pending review).",
                TenantId: tenant.Id,
                ActorType: "Student",
                Portal: "student"),
            cancellationToken);

        return new StudentRegistrationResultDto(student.Id, student.Status);
    }
}
