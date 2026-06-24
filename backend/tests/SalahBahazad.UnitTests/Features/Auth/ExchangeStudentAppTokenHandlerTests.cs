using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentAppToken;
using SalahBahazad.Domain.Enums;
using static SalahBahazad.UnitTests.Features.Auth.AuthTestHelpers;

namespace SalahBahazad.UnitTests.Features.Auth;

/// <summary>
/// The device-agnostic native-app exchange (contract §A): the same status gate as the portal exchange, but
/// with <b>no</b> device binding and tokens carrying <b>no</b> <c>device_id</c>. Shares the verify + lookup +
/// status-gate + sign-in audit path with the portal handler via <c>StudentSignInHandlerBase</c>.
/// </summary>
public class ExchangeStudentAppTokenHandlerTests
{
    private static ExchangeStudentAppTokenHandler Build(
        IAppDbContext db,
        IFirebaseAuthService firebase,
        IAuditWriter audit,
        IJwtTokenService? jwt = null) =>
        new(firebase, jwt ?? Jwt(), audit, db, TimeProvider.System,
            NullLogger<ExchangeStudentAppTokenHandler>.Instance);

    private static ExchangeStudentAppTokenCommand Command(string idToken = "id-token") => new(idToken);

    // ── Device-agnostic happy path (contract §A.1, FR-APP-DEV-001) ─────────────────

    [Fact]
    public async Task Active_student_signs_in_with_no_device_id_and_a_null_bound_device()
    {
        var student = NewStudent();
        var jwt = Jwt();
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith([student]), Firebase(student.FirebaseUid), audit, jwt);

        var response = await handler.Handle(Command(), CancellationToken.None);

        response.Student.Id.Should().Be(student.Id);
        response.Student.Status.Should().Be(StudentStatus.Active);
        response.Student.BoundDevice.Should().BeNull("the app session binds no device (contract §A.1)");
        response.AccessToken.Should().StartWith("access-");
        response.RefreshToken.Should().StartWith("refresh-");
        student.LastSeenAtUtc.Should().NotBeNull();

        // Tokens are minted with deviceId: null → no device_id claim (the whole device-agnostic trick).
        jwt.Received(1).IssueStudentAccessToken(student, null);
        jwt.Received(1).IssueStudentRefreshToken(student, null);

        // Audited: exactly one StudentSignedIn (Student actor, "app" portal) and NEVER StudentDeviceBound (§I).
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r =>
                r.Action == "StudentSignedIn" && r.ActorType == "Student" && r.Portal == "app"
                && r.EntityId == student.Id),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentDeviceBound"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_student_already_device_bound_via_the_portal_still_signs_into_the_app()
    {
        // Headline: a portal-bound student (an active StudentDevice exists) signs into the app from anywhere.
        // The app path never reads or binds a device, so the existing binding is irrelevant — no 403.
        var student = NewStudent();
        var device = ActiveDevice(student);
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith([student], [device]), Firebase(student.FirebaseUid), audit);

        var response = await handler.Handle(Command(), CancellationToken.None);

        response.Student.BoundDevice.Should().BeNull();
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentSignedIn" && r.Portal == "app"),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentDeviceBound"), Arg.Any<CancellationToken>());
    }

    // ── Status gate (FR-PLAT-AUTH-005), shared with the portal path ────────────────

    [Theory]
    [InlineData(StudentStatus.Pending, "account_pending")]
    [InlineData(StudentStatus.Inactive, "account_inactive")]
    public async Task Blocked_status_throws_the_right_reason_and_audits_a_rejection(
        StudentStatus status, string expectedReason)
    {
        var student = NewStudent(status);
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith([student]), Firebase(student.FirebaseUid), audit);

        var act = () => handler.Handle(Command(), CancellationToken.None).AsTask();

        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.Reason.Should().Be(expectedReason);
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r =>
                r.Action == "StudentSignInRejected"
                && r.ActorType == "Student"
                && r.Portal == "app"
                && r.TenantId == student.TenantId
                && r.EntityId == student.Id
                && r.Summary.Contains(expectedReason)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejected_status_surfaces_the_stored_rejection_reason()
    {
        var student = NewStudent(StudentStatus.Rejected, rejectionReason: "Your ID photo was unreadable.");
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith([student]), Firebase(student.FirebaseUid), audit);

        var act = () => handler.Handle(Command(), CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Reason.Should().Be("account_rejected");
        ex.Which.Message.Should().Be("Your ID photo was unreadable.");
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentSignInRejected" && r.Portal == "app"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_student_account_throws_401_and_writes_no_audit()
    {
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith(students: []), Firebase("nobody-uid"), audit);

        var act = () => handler.Handle(Command(), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await audit.DidNotReceive().WriteAsync(Arg.Any<AuditWriteRequest>(), Arg.Any<CancellationToken>());
    }

    // ── Validator (§7: "validator rejects empty token") ────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validator_rejects_a_blank_firebase_token(string token)
    {
        var result = new ExchangeStudentAppTokenValidator().Validate(new ExchangeStudentAppTokenCommand(token));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ExchangeStudentAppTokenCommand.FirebaseIdToken));
    }

    [Fact]
    public void Validator_accepts_a_non_empty_firebase_token()
    {
        new ExchangeStudentAppTokenValidator()
            .Validate(new ExchangeStudentAppTokenCommand("id-token"))
            .IsValid.Should().BeTrue();
    }
}
