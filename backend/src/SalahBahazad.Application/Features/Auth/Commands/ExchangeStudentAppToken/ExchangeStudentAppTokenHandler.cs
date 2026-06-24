using Mediator;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentAppToken;

/// <summary>
/// The <b>device-agnostic app</b> student exchange (contract §A): verifies the Firebase ID token, resolves
/// the student, enforces the status gate, records the sign-in, and issues a Student-role JWT pair carrying
/// <b>no</b> <c>device_id</c> — performing <b>no</b> device binding (no cookie, no fingerprint, no
/// <c>StudentDevice</c> read/write). One-device binding stays a portal concern; the app's anti-sharing is the
/// watermark + view cap, not device identity (FR-APP-DEV-001/002). The verify + lookup + status-gate +
/// <c>StudentSignedIn</c> audit + token-issue path is shared with the portal exchange via
/// <see cref="StudentSignInHandlerBase"/> so the two cannot drift; this handler only omits the device work
/// and issues <c>BoundDevice = null</c>. Audits <c>StudentSignedIn</c> / <c>StudentSignInRejected</c> only —
/// never <c>StudentDeviceBound</c> (§I). Runs anonymously, so audit rows are written explicitly.
/// </summary>
internal sealed class ExchangeStudentAppTokenHandler(
    IFirebaseAuthService firebaseAuth,
    IJwtTokenService jwtTokenService,
    IAuditWriter auditWriter,
    IAppDbContext db,
    TimeProvider clock,
    ILogger<ExchangeStudentAppTokenHandler> logger)
    : StudentSignInHandlerBase(firebaseAuth, jwtTokenService, auditWriter, db, clock, logger),
      IRequestHandler<ExchangeStudentAppTokenCommand, StudentAuthResponse>
{
    protected override string Portal => "app";

    public async ValueTask<StudentAuthResponse> Handle(
        ExchangeStudentAppTokenCommand command,
        CancellationToken cancellationToken)
    {
        var student = await AuthenticateActiveStudentAsync(command.FirebaseIdToken, cancellationToken);

        // No device work: an Active student signs in from any machine, with no binding row touched.
        var now = Clock.GetUtcNow();

        return await Db.ExecuteInTransactionAsync(async () =>
        {
            student.RecordSignIn(now);
            await Db.SaveChangesAsync(cancellationToken);

            await WriteSignedInAuditAsync(student, cancellationToken);

            // device: null → tokens carry no device_id and BoundDevice is null (contract §A.1).
            var response = IssueTokensAndBuildResponse(student, device: null);

            Logger.LogInformation("Student {StudentId} signed in via the app (device-agnostic)", student.Id);

            return response;
        }, cancellationToken);
    }
}
