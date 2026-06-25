using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A student within a tenant. Identity lives in Firebase — the platform stores no passwords
/// (FR-PLAT-AUTH-004). Created <see cref="StudentStatus.Pending"/> by self-registration
/// (FR-STU-REG-001..008) and moved through its lifecycle by staff (FR-ADM-STU-003..006).
/// Soft-deleted so audit, attendance, and enrollment history survive (FR-PLAT-ROLE-004).
/// </summary>
public sealed class Student : TenantEntityBase, ISoftDeletable
{
    private Student() { }

    public string FirebaseUid { get; private set; } = string.Empty;

    /// <summary>
    /// Randomly-generated, tenant-unique watermark identity in the form <c>STU-XXXXXX</c> (FR-APP-VID-003).
    /// Minted once at <see cref="Register"/> and never changed — the native player renders "{Serial} · {FullName}"
    /// as the anti-sharing watermark (it replaces device binding for the app). Stable across a reject→resubmit cycle.
    /// </summary>
    public string Serial { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>The student's own contact phone number (FR-STU-REG-004).</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>Required parent/guardian phone for supervision (FR-STU-REG-004).</summary>
    public string ParentPhonePrimary { get; private set; } = string.Empty;

    /// <summary>Optional second parent/guardian phone (FR-STU-REG-004).</summary>
    public string? ParentPhoneSecondary { get; private set; }

    /// <summary>Teacher-managed grade (FR-PLAT-TAX-001), tenant-scoped reference.</summary>
    public Guid GradeId { get; private set; }

    /// <summary>City from the seeded global Egypt dataset (FR-PLAT-TAX-003).</summary>
    public Guid CityId { get; private set; }

    /// <summary>Region from the seeded global Egypt dataset; belongs to <see cref="CityId"/>.</summary>
    public Guid RegionId { get; private set; }

    public string SchoolName { get; private set; } = string.Empty;

    /// <summary>
    /// R2 object key for the ID-verification image (FR-PLAT-AST-004) — a key only, never bytes or a
    /// durable URL. Served exclusively via short-lived signed URLs after an audited access
    /// (FR-PLAT-AST-003). Null until the registration upload completes.
    /// </summary>
    public string? IdImageObjectKey { get; private set; }

    public StudentStatus Status { get; private set; } = StudentStatus.Pending;

    /// <summary>Mandatory reason captured when a registration is rejected (FR-ADM-STU-004).</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>When terms &amp; conditions were accepted during registration (FR-STU-REG-006).</summary>
    public DateTimeOffset? TermsAcceptedAtUtc { get; private set; }

    /// <summary>Version of the terms accepted, for an auditable consent record (FR-STU-REG-006).</summary>
    public string? TermsVersion { get; private set; }

    /// <summary>Timestamp of the most recent successful sign-in; null until the student first logs in.</summary>
    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Creates a self-registered student in <see cref="StudentStatus.Pending"/> with their
    /// terms-acceptance consent recorded (FR-STU-REG-001..008). The ID-image key is attached
    /// separately once the upload to R2 succeeds (<see cref="AttachIdImage"/>).
    /// </summary>
    public static Student Register(
        Guid tenantId,
        string firebaseUid,
        string serial,
        string fullName,
        string phoneNumber,
        string parentPhonePrimary,
        string? parentPhoneSecondary,
        Guid gradeId,
        Guid cityId,
        Guid regionId,
        string schoolName,
        string termsVersion,
        DateTimeOffset termsAcceptedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseUid);
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentPhonePrimary);
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(termsVersion);
        if (gradeId == Guid.Empty) throw new ArgumentException("A student must have a grade.", nameof(gradeId));
        if (cityId == Guid.Empty) throw new ArgumentException("A student must have a city.", nameof(cityId));
        if (regionId == Guid.Empty) throw new ArgumentException("A student must have a region.", nameof(regionId));

        var student = new Student
        {
            FirebaseUid = firebaseUid,
            Serial = serial,
            FullName = fullName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            ParentPhonePrimary = parentPhonePrimary.Trim(),
            ParentPhoneSecondary = string.IsNullOrWhiteSpace(parentPhoneSecondary) ? null : parentPhoneSecondary.Trim(),
            GradeId = gradeId,
            CityId = cityId,
            RegionId = regionId,
            SchoolName = schoolName.Trim(),
            Status = StudentStatus.Pending,
            TermsVersion = termsVersion.Trim(),
            TermsAcceptedAtUtc = termsAcceptedAtUtc,
        };
        student.SetTenant(tenantId);
        return student;
    }

    /// <summary>Records the R2 key of the uploaded ID-verification image (FR-PLAT-AST-004).</summary>
    public void AttachIdImage(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        IdImageObjectKey = objectKey;
    }

    /// <summary>Approves a pending registration, enabling sign-in (FR-ADM-STU-003).</summary>
    public void Approve()
    {
        if (Status != StudentStatus.Pending)
            throw new InvalidOperationException(
                $"Only a pending student can be approved; current status is {Status} (FR-ADM-STU-003).");

        Status = StudentStatus.Active;
        RejectionReason = null;
        AddDomainEvent(new StudentApprovedEvent(Id));
    }

    /// <summary>Rejects a pending registration; a reason is mandatory and stored (FR-ADM-STU-004).</summary>
    public void Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != StudentStatus.Pending)
            throw new InvalidOperationException(
                $"Only a pending student can be rejected; current status is {Status} (FR-ADM-STU-004).");

        Status = StudentStatus.Rejected;
        RejectionReason = reason.Trim();
        AddDomainEvent(new StudentRejectedEvent(Id, RejectionReason));
    }

    /// <summary>
    /// A <see cref="StudentStatus.Rejected"/> registration is corrected and re-submitted for review,
    /// reusing the same row (and the same Firebase identity) so the email is never a dead-end
    /// (FR-ADM-STU-004 follow-up). Overwrites the editable details with the new submission, clears the
    /// rejection reason, and returns to <see cref="StudentStatus.Pending"/>. The fresh ID image is
    /// attached separately via <see cref="AttachIdImage"/> once its upload succeeds. Like
    /// <see cref="Register"/>, this runs in the anonymous self-service path, so it raises no domain event —
    /// the audit row is written explicitly by the handler with the resolved tenant.
    /// </summary>
    public void Resubmit(
        string fullName,
        string phoneNumber,
        string parentPhonePrimary,
        string? parentPhoneSecondary,
        Guid gradeId,
        Guid cityId,
        Guid regionId,
        string schoolName,
        string termsVersion,
        DateTimeOffset termsAcceptedAtUtc)
    {
        if (Status != StudentStatus.Rejected)
            throw new InvalidOperationException(
                $"Only a rejected registration can be re-submitted; current status is {Status}.");

        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentPhonePrimary);
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(termsVersion);
        if (gradeId == Guid.Empty) throw new ArgumentException("A student must have a grade.", nameof(gradeId));
        if (cityId == Guid.Empty) throw new ArgumentException("A student must have a city.", nameof(cityId));
        if (regionId == Guid.Empty) throw new ArgumentException("A student must have a region.", nameof(regionId));

        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        ParentPhonePrimary = parentPhonePrimary.Trim();
        ParentPhoneSecondary = string.IsNullOrWhiteSpace(parentPhoneSecondary) ? null : parentPhoneSecondary.Trim();
        GradeId = gradeId;
        CityId = cityId;
        RegionId = regionId;
        SchoolName = schoolName.Trim();
        TermsVersion = termsVersion.Trim();
        TermsAcceptedAtUtc = termsAcceptedAtUtc;

        Status = StudentStatus.Pending;
        RejectionReason = null;
        // Serial is intentionally NOT touched — the watermark identity is minted once at Register and is stable
        // across a reject→resubmit cycle (FR-APP-VID-003).
    }

    /// <summary>Deactivates an active account; sign-in is refused while inactive (FR-ADM-STU-006).</summary>
    public void Deactivate()
    {
        if (Status != StudentStatus.Active)
            throw new InvalidOperationException(
                $"Only an active student can be deactivated; current status is {Status} (FR-ADM-STU-006).");

        Status = StudentStatus.Inactive;
        AddDomainEvent(new StudentDeactivatedEvent(Id));
    }

    /// <summary>Re-activates a deactivated account (FR-ADM-STU-006).</summary>
    public void Reactivate()
    {
        if (Status != StudentStatus.Inactive)
            throw new InvalidOperationException(
                $"Only an inactive student can be re-activated; current status is {Status} (FR-ADM-STU-006).");

        Status = StudentStatus.Active;
        AddDomainEvent(new StudentReactivatedEvent(Id));
    }

    /// <summary>Staff correction of grade and the student/parent contact numbers (FR-ADM-STU-005).</summary>
    public void UpdateContactInfo(
        Guid gradeId, string phoneNumber, string parentPhonePrimary, string? parentPhoneSecondary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentPhonePrimary);
        if (gradeId == Guid.Empty) throw new ArgumentException("A student must have a grade.", nameof(gradeId));

        GradeId = gradeId;
        PhoneNumber = phoneNumber.Trim();
        ParentPhonePrimary = parentPhonePrimary.Trim();
        ParentPhoneSecondary = string.IsNullOrWhiteSpace(parentPhoneSecondary) ? null : parentPhoneSecondary.Trim();
    }

    /// <summary>
    /// The student edits their <b>own</b> profile from the student portal (FR-STU-PRO-001/002, Student-Portal S6).
    /// Updates the seven self-service fields — name, contact + parent/guardian phones, school, city, region — and
    /// deliberately leaves <see cref="GradeId"/> UNCHANGED: grade is staff-managed (FR-ADM-STU-005), so unlike the
    /// staff-side <see cref="UpdateContactInfo"/> this method takes no <c>gradeId</c>. Email is the Firebase identity
    /// and is not stored here at all (Student-Portal S6 §C.2). The city/region pair's existence + belongs-to-city is
    /// validated by the handler before this is called (a 400, Student-Portal S6 §C.3); this method only enforces the
    /// shape invariants the entity owns.
    /// </summary>
    public void UpdateOwnProfile(
        string fullName,
        string phoneNumber,
        string schoolName,
        Guid cityId,
        Guid regionId,
        string parentPhonePrimary,
        string? parentPhoneSecondary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentPhonePrimary);
        if (cityId == Guid.Empty) throw new ArgumentException("A student must have a city.", nameof(cityId));
        if (regionId == Guid.Empty) throw new ArgumentException("A student must have a region.", nameof(regionId));

        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        SchoolName = schoolName.Trim();
        CityId = cityId;
        RegionId = regionId;
        ParentPhonePrimary = parentPhonePrimary.Trim();
        ParentPhoneSecondary = string.IsNullOrWhiteSpace(parentPhoneSecondary) ? null : parentPhoneSecondary.Trim();
        // GradeId is intentionally NOT touched — a student cannot change their own grade (FR-ADM-STU-005).
    }

    /// <summary>Stamps a successful sign-in, surfaced as "Last active" to staff (FR-ADM-STU-002).</summary>
    public void RecordSignIn(DateTimeOffset now) => LastSeenAtUtc = now;

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
    }
}
