using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;

/// <summary>
/// Verifies the Firebase ID token, looks up the <b>student</b> account (cross-tenant — sign-in carries no
/// tenant claim), enforces the status gate (FR-PLAT-AUTH-005) and one-device binding
/// (FR-PLAT-DEV-001/003), records the sign-in, and issues a Student-role JWT pair. Every outcome where a
/// student is resolved is audited (success, new bind, or rejection — §1.5 / FR-PLAT-AUD-002); the
/// (no account) case is logged only, as there is no tenant to attribute it to. Runs anonymously, so the
/// audit interceptor is a no-op and audit rows are written explicitly via <see cref="IAuditWriter"/>
/// (the same pattern as <c>RegisterStudentHandler</c>).
/// </summary>
internal sealed class ExchangeStudentFirebaseTokenHandler(
    IFirebaseAuthService firebaseAuth,
    IJwtTokenService jwtTokenService,
    IDeviceBindingService deviceBinding,
    IAuditWriter auditWriter,
    IAppDbContext db,
    TimeProvider clock,
    ILogger<ExchangeStudentFirebaseTokenHandler> logger)
    : IRequestHandler<ExchangeStudentFirebaseTokenCommand, StudentExchangeResult>
{
    // Machine reason codes (frozen contract §1.4) — surfaced as the ProblemDetails `reason` extension so the
    // student portal can map each to its copy verbatim. The readable detail rides the exception message.
    private const string ReasonPending = "account_pending";
    private const string ReasonRejected = "account_rejected";
    private const string ReasonInactive = "account_inactive";
    private const string ReasonDeviceNotRecognized = "device_not_recognized";

    private const string DetailPending = "Your account is pending approval. Your teacher will review it soon.";
    private const string DetailInactive = "Your account has been deactivated. Contact support.";
    private const string DetailDeviceNotRecognized =
        "This device isn't recognised. Contact support to reset your bound device.";

    public async ValueTask<StudentExchangeResult> Handle(
        ExchangeStudentFirebaseTokenCommand command,
        CancellationToken cancellationToken)
    {
        var claims = await firebaseAuth.VerifyIdTokenAsync(command.FirebaseIdToken, cancellationToken);

        // Sign-in is cross-tenant: no tenant claim yet, so the global TenantId filter would hide every row.
        // The Firebase UID identifies the account; we discover the tenant FROM the row. IgnoreQueryFilters
        // also drops the soft-delete filter — a soft-deleted student is treated as "no account" → 401.
        // Tracked (not AsNoTracking) so RecordSignIn persists below.
        var student = await db.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.FirebaseUid == claims.Uid, cancellationToken);

        if (student is null)
        {
            // No tenant/student to attribute → logged, not audited (§1.5).
            logger.LogWarning("Firebase UID {Uid} has no student account — sign-in rejected", claims.Uid);
            throw new UnauthorizedAccessException("This account doesn't have student access.");
        }

        // ── Status gate (FR-PLAT-AUTH-005) ─────────────────────────────────────────────
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

        // ── Device enforcement decision (reads only) ───────────────────────────────────
        // Decided BEFORE opening a transaction so a rejection's audit row commits — a throw inside the
        // transaction would roll it back. The actual bind/sign-in mutations run in the transaction below.
        var activeDevice = await db.StudentDevices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.StudentId == student.Id && d.IsActive, cancellationToken);

        if (activeDevice is not null)
        {
            var presentedHash = string.IsNullOrEmpty(command.RawDeviceToken)
                ? null
                : deviceBinding.Verify(command.RawDeviceToken);

            if (presentedHash is null
                || !string.Equals(presentedHash, activeDevice.DeviceTokenHash, StringComparison.Ordinal))
            {
                throw await RejectedAsync(
                    student, ReasonDeviceNotRecognized, DetailDeviceNotRecognized, cancellationToken);
            }
        }

        // ── Bind / reuse + sign-in (atomic) ────────────────────────────────────────────
        var now = clock.GetUtcNow();
        var isNewBind = activeDevice is null;

        return await db.ExecuteInTransactionAsync(async () =>
        {
            StudentDevice device;
            string deviceTokenToSet;

            if (activeDevice is null)
            {
                // First sign-in / post-staff-clear: mint a token, persist only its hash (FR-PLAT-DEV-005).
                var (rawToken, hash) = deviceBinding.Issue(student.Id, Guid.CreateVersion7());
                device = StudentDevice.Bind(
                    student.TenantId, student.Id, hash, deviceBinding.Summarize(command.Fingerprint), now);
                db.StudentDevices.Add(device);
                deviceTokenToSet = rawToken;
            }
            else
            {
                // Recognised device: keep the binding, re-present the same token to slide the cookie's expiry.
                device = activeDevice;
                deviceTokenToSet = command.RawDeviceToken!;
            }

            student.RecordSignIn(now);
            await db.SaveChangesAsync(cancellationToken);

            if (isNewBind)
            {
                await auditWriter.WriteAsync(
                    new AuditWriteRequest(
                        Action: "StudentDeviceBound",
                        EntityType: nameof(StudentDevice),
                        EntityId: device.Id,
                        Summary: $"Device bound ({device.FingerprintSummary ?? "unknown device"}) for {student.FullName}.",
                        TenantId: student.TenantId,
                        ActorType: "Student",
                        Portal: "student"),
                    cancellationToken);
            }

            await auditWriter.WriteAsync(
                new AuditWriteRequest(
                    Action: "StudentSignedIn",
                    EntityType: nameof(Student),
                    EntityId: student.Id,
                    Summary: $"{student.FullName } signed in.",
                    TenantId: student.TenantId,
                    ActorType: "Student",
                    Portal: "student"),
                cancellationToken);

            var accessToken = jwtTokenService.IssueStudentAccessToken(student, device.Id);
            var refreshToken = jwtTokenService.IssueStudentRefreshToken(student, device.Id);

            logger.LogInformation(
                "Student {StudentId} signed in (device {DeviceId}, newBind={NewBind})",
                student.Id, device.Id, isNewBind);

            var response = new StudentAuthResponse(
                accessToken.Value,
                refreshToken.Value,
                accessToken.ExpiresAt,
                refreshToken.ExpiresAt,
                new StudentInfo(
                    student.Id,
                    student.FullName,
                    student.Status,
                    new BoundDeviceInfo(device.FingerprintSummary, device.BoundAtUtc)));

            return new StudentExchangeResult(response, deviceTokenToSet);
        }, cancellationToken);
    }

    /// <summary>
    /// Audits a blocked attempt where a student was resolved (§1.5) and returns the matching 403 to throw.
    /// The audit row commits immediately (its own SaveChanges, no ambient transaction) so it survives the
    /// throw. Callers use <c>throw await RejectedAsync(...)</c> so control flow is explicit at the call site.
    /// </summary>
    private async Task<ForbiddenException> RejectedAsync(
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
                Portal: "student"),
            cancellationToken);

        logger.LogWarning("Student {StudentId} sign-in rejected ({Reason})", student.Id, reason);
        return new ForbiddenException(detail, reason);
    }
}
