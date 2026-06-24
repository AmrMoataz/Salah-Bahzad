using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Auth;

/// <summary>
/// The sign-in path shared by the two student token exchanges — the device-bound <b>portal</b> exchange
/// (<c>ExchangeStudentFirebaseTokenHandler</c>) and the device-agnostic <b>app</b> exchange
/// (<c>ExchangeStudentAppTokenHandler</c>). Holds the parts that <i>must not drift</i> between them:
/// Firebase verification, the cross-tenant student lookup, the status gate (FR-PLAT-AUTH-005) with its
/// rejection audit, the <c>StudentSignedIn</c> audit, and student JWT issuance + response shape. The only
/// difference is the device: the portal path binds/enforces one, the app path passes <c>null</c> — so app
/// tokens carry no <c>device_id</c> and <c>BoundDevice</c> is <c>null</c> (contract §A, FR-APP-DEV-001).
/// The per-path <see cref="Portal"/> ("student" / "app") flows into every audit row this base writes.
/// Runs anonymously (no JWT tenant claim), so the audit interceptor is a no-op and rows are written
/// explicitly via <see cref="IAuditWriter"/>, scoped to the tenant discovered on the student row (§1.5).
/// </summary>
internal abstract class StudentSignInHandlerBase(
    IFirebaseAuthService firebaseAuth,
    IJwtTokenService jwtTokenService,
    IAuditWriter auditWriter,
    IAppDbContext db,
    TimeProvider clock,
    ILogger logger)
{
    // Machine reason codes (frozen contract §1.4 / §A.2) — surfaced as the ProblemDetails `reason` extension
    // so each client maps it to its copy verbatim. The readable detail rides the exception message.
    private const string ReasonPending = "account_pending";
    private const string ReasonRejected = "account_rejected";
    private const string ReasonInactive = "account_inactive";

    private const string DetailPending = "Your account is pending approval. Your teacher will review it soon.";
    private const string DetailInactive = "Your account has been deactivated. Contact support.";

    protected IAppDbContext Db => db;
    protected TimeProvider Clock => clock;
    protected IAuditWriter AuditWriter => auditWriter;
    protected ILogger Logger => logger;

    /// <summary>The audit <c>Portal</c> attributed to every row this sign-in writes ("student" / "app").</summary>
    protected abstract string Portal { get; }

    /// <summary>
    /// Verifies the Firebase ID token, resolves the <b>student</b> by Firebase UID (cross-tenant — sign-in
    /// carries no tenant claim, so the global filter is bypassed; a soft-deleted student is treated as
    /// "no account" → 401), and enforces the status gate (FR-PLAT-AUTH-005), auditing a blocked attempt.
    /// Returns the <see cref="StudentStatus.Active"/> student (tracked, so the caller can record the sign-in).
    /// </summary>
    protected async Task<Student> AuthenticateActiveStudentAsync(
        string firebaseIdToken, CancellationToken cancellationToken)
    {
        var claims = await firebaseAuth.VerifyIdTokenAsync(firebaseIdToken, cancellationToken);

        var student = await db.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.FirebaseUid == claims.Uid, cancellationToken);

        if (student is null)
        {
            // No tenant/student to attribute → logged, not audited (§1.5).
            logger.LogWarning("Firebase UID {Uid} has no student account — sign-in rejected", claims.Uid);
            throw new UnauthorizedAccessException("This account doesn't have student access.");
        }

        if (student.Status != StudentStatus.Active)
        {
            var (reason, detail) = student.Status switch
            {
                StudentStatus.Pending => (ReasonPending, DetailPending),
                StudentStatus.Rejected =>
                    (ReasonRejected, student.RejectionReason ?? "Your registration was not approved."),
                StudentStatus.Inactive => (ReasonInactive, DetailInactive),
                _ => throw new InvalidOperationException($"Unhandled student status {student.Status}."),
            };
            throw await RejectedAsync(student, reason, detail, cancellationToken);
        }

        return student;
    }

    /// <summary>
    /// Audits a blocked attempt where a student was resolved (§1.5) and returns the matching 403 to throw.
    /// The audit row commits immediately (its own SaveChanges, no ambient transaction) so it survives the
    /// throw. Callers use <c>throw await RejectedAsync(...)</c> so control flow is explicit at the call site.
    /// </summary>
    protected async Task<ForbiddenException> RejectedAsync(
        Student student, string reason, string detail, CancellationToken cancellationToken)
    {
        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "StudentSignInRejected",
                EntityType: nameof(Student),
                EntityId: student.Id,
                Summary: $"{student.FullName} Sign-in rejected: {reason}",
                TenantId: student.TenantId,
                ActorType: "Student",
                Portal: Portal),
            cancellationToken);

        logger.LogWarning("Student {StudentId} sign-in rejected ({Reason})", student.Id, reason);
        return new ForbiddenException(detail, reason);
    }

    /// <summary>Writes the single <c>StudentSignedIn</c> row (Student actor, this path's <see cref="Portal"/>).</summary>
    protected Task WriteSignedInAuditAsync(Student student, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            new AuditWriteRequest(
                Action: "StudentSignedIn",
                EntityType: nameof(Student),
                EntityId: student.Id,
                Summary: $"{student.FullName} signed in.",
                TenantId: student.TenantId,
                ActorType: "Student",
                Portal: Portal),
            cancellationToken);

    /// <summary>
    /// Issues the student access + refresh JWT pair and builds the wire response. When <paramref name="device"/>
    /// is supplied (portal) the tokens carry its id as <c>device_id</c> and <c>BoundDevice</c> describes it;
    /// when <c>null</c> (app) the tokens carry no <c>device_id</c> and <c>BoundDevice</c> is <c>null</c>
    /// (contract §A.1, FR-APP-DEV-001).
    /// </summary>
    protected StudentAuthResponse IssueTokensAndBuildResponse(Student student, StudentDevice? device)
    {
        var accessToken = jwtTokenService.IssueStudentAccessToken(student, device?.Id);
        var refreshToken = jwtTokenService.IssueStudentRefreshToken(student, device?.Id);

        return new StudentAuthResponse(
            accessToken.Value,
            refreshToken.Value,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt,
            new StudentInfo(
                student.Id,
                student.FullName,
                student.Status,
                device is null ? null : new BoundDeviceInfo(device.FingerprintSummary, device.BoundAtUtc)));
    }
}
