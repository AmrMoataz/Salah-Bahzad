using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;

/// <summary>
/// The <b>portal</b> student exchange: verifies the Firebase ID token, resolves the student, enforces the
/// status gate (FR-PLAT-AUTH-005) and the one-device binding (FR-PLAT-DEV-001/003), records the sign-in,
/// and issues a device-bound Student-role JWT pair. The shared sign-in path (verify + lookup + status gate +
/// <c>StudentSignedIn</c> audit + token issuance) lives in <see cref="StudentSignInHandlerBase"/> so this
/// device-bound path and the device-agnostic app path (<c>ExchangeStudentAppTokenHandler</c>) cannot drift;
/// only the device work below is unique to the portal. Runs anonymously, so audit rows are written
/// explicitly via <see cref="IAuditWriter"/> (the same pattern as <c>RegisterStudentHandler</c>).
/// </summary>
internal sealed class ExchangeStudentFirebaseTokenHandler(
    IFirebaseAuthService firebaseAuth,
    IJwtTokenService jwtTokenService,
    IDeviceBindingService deviceBinding,
    IAuditWriter auditWriter,
    IAppDbContext db,
    TimeProvider clock,
    ILogger<ExchangeStudentFirebaseTokenHandler> logger)
    : StudentSignInHandlerBase(firebaseAuth, jwtTokenService, auditWriter, db, clock, logger),
      IRequestHandler<ExchangeStudentFirebaseTokenCommand, StudentExchangeResult>
{
    private const string ReasonDeviceNotRecognized = "device_not_recognized";
    private const string DetailDeviceNotRecognized =
        "This device isn't recognised. Contact support to reset your bound device.";

    protected override string Portal => "student";

    public async ValueTask<StudentExchangeResult> Handle(
        ExchangeStudentFirebaseTokenCommand command,
        CancellationToken cancellationToken)
    {
        var student = await AuthenticateActiveStudentAsync(command.FirebaseIdToken, cancellationToken);

        // ── Device enforcement decision (reads only) ───────────────────────────────────
        // Decided BEFORE opening a transaction so a rejection's audit row commits — a throw inside the
        // transaction would roll it back. The actual bind/sign-in mutations run in the transaction below.
        var activeDevice = await Db.StudentDevices
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
        var now = Clock.GetUtcNow();
        var isNewBind = activeDevice is null;

        return await Db.ExecuteInTransactionAsync(async () =>
        {
            StudentDevice device;
            string deviceTokenToSet;

            if (activeDevice is null)
            {
                // First sign-in / post-staff-clear: mint a token, persist only its hash (FR-PLAT-DEV-005).
                var (rawToken, hash) = deviceBinding.Issue(student.Id, Guid.CreateVersion7());
                device = StudentDevice.Bind(
                    student.TenantId, student.Id, hash, deviceBinding.Summarize(command.Fingerprint), now);
                Db.StudentDevices.Add(device);
                deviceTokenToSet = rawToken;
            }
            else
            {
                // Recognised device: keep the binding, re-present the same token to slide the cookie's expiry.
                device = activeDevice;
                deviceTokenToSet = command.RawDeviceToken!;
            }

            student.RecordSignIn(now);
            await Db.SaveChangesAsync(cancellationToken);

            if (isNewBind)
            {
                await AuditWriter.WriteAsync(
                    new AuditWriteRequest(
                        Action: "StudentDeviceBound",
                        EntityType: nameof(StudentDevice),
                        EntityId: device.Id,
                        Summary: $"Device bound ({device.FingerprintSummary ?? "unknown device"}) for {student.FullName}.",
                        TenantId: student.TenantId,
                        ActorType: "Student",
                        Portal: Portal),
                    cancellationToken);
            }

            await WriteSignedInAuditAsync(student, cancellationToken);

            var response = IssueTokensAndBuildResponse(student, device);

            Logger.LogInformation(
                "Student {StudentId} signed in (device {DeviceId}, newBind={NewBind})",
                student.Id, device.Id, isNewBind);

            return new StudentExchangeResult(response, deviceTokenToSet);
        }, cancellationToken);
    }
}
