using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using static SalahBahazad.UnitTests.Features.Auth.AuthTestHelpers;

namespace SalahBahazad.UnitTests.Features.Auth;

public class ExchangeStudentFirebaseTokenHandlerTests
{
    private static ExchangeStudentFirebaseTokenHandler Build(
        IAppDbContext db,
        IFirebaseAuthService firebase,
        IDeviceBindingService binding,
        IAuditWriter audit,
        IJwtTokenService? jwt = null) =>
        new(firebase, jwt ?? Jwt(), binding, audit, db, TimeProvider.System,
            NullLogger<ExchangeStudentFirebaseTokenHandler>.Instance);

    private static ExchangeStudentFirebaseTokenCommand Command(
        string idToken = "id-token", string? rawDeviceToken = null, string? fingerprint = null) =>
        new(idToken, rawDeviceToken, fingerprint, "203.0.113.5");

    // ── Status gate (FR-PLAT-AUTH-005) ─────────────────────────────────────────────

    [Theory]
    [InlineData(StudentStatus.Pending, "account_pending")]
    [InlineData(StudentStatus.Inactive, "account_inactive")]
    public async Task Blocked_status_throws_the_right_reason_and_audits_a_rejection(
        StudentStatus status, string expectedReason)
    {
        var student = NewStudent(status);
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith([student]), Firebase(student.FirebaseUid), Binding(), audit);

        var act = () => handler.Handle(Command(), CancellationToken.None).AsTask();

        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.Reason.Should().Be(expectedReason);
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r =>
                r.Action == "StudentSignInRejected"
                && r.ActorType == "Student"
                && r.Portal == "student"
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
        var handler = Build(DbWith([student]), Firebase(student.FirebaseUid), Binding(), audit);

        var act = () => handler.Handle(Command(), CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Reason.Should().Be("account_rejected");
        ex.Which.Message.Should().Be("Your ID photo was unreadable.");
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentSignInRejected"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_student_account_throws_401_and_writes_no_audit()
    {
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(DbWith(students: []), Firebase("nobody-uid"), Binding(), audit);

        var act = () => handler.Handle(Command(), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await audit.DidNotReceive().WriteAsync(Arg.Any<AuditWriteRequest>(), Arg.Any<CancellationToken>());
    }

    // ── Device bind / enforce (FR-PLAT-DEV-001/003) ────────────────────────────────

    [Fact]
    public async Task First_sign_in_binds_a_new_active_device_and_audits_bound_then_signed_in()
    {
        var student = NewStudent();
        var db = DbWith([student]); // no devices
        var audit = Substitute.For<IAuditWriter>();
        var binding = Binding(issuedRawToken: "fresh-token", issuedHash: "fresh-hash");
        var handler = Build(db, Firebase(student.FirebaseUid), binding, audit);

        var result = await handler.Handle(
            Command(fingerprint: "Android · Chrome"), CancellationToken.None);

        result.DeviceTokenToSet.Should().Be("fresh-token");
        result.Response.AccessToken.Should().StartWith("access-");
        result.Response.RefreshToken.Should().StartWith("refresh-");
        result.Response.Student.Id.Should().Be(student.Id);
        result.Response.Student.Status.Should().Be(StudentStatus.Active);
        result.Response.Student.BoundDevice!.Summary.Should().Be("Android · Chrome");

        db.StudentDevices.Received(1).Add(Arg.Is<StudentDevice>(d =>
            d.IsActive && d.StudentId == student.Id && d.DeviceTokenHash == "fresh-hash"
            && d.FingerprintSummary == "Android · Chrome"));
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentDeviceBound" && r.ActorType == "Student"),
            Arg.Any<CancellationToken>());
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r =>
                r.Action == "StudentSignedIn" && r.ActorType == "Student" && r.Portal == "student"
                && r.EntityId == student.Id),
            Arg.Any<CancellationToken>());
        student.LastSeenAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Matching_cookie_reuses_the_device_without_a_second_bind()
    {
        var student = NewStudent();
        var device = ActiveDevice(student, hash: "stored-hash", fingerprint: "iOS 18 · Safari");
        var db = DbWith([student], [device]);
        var audit = Substitute.For<IAuditWriter>();
        var binding = Binding();
        binding.Verify("good-token").Returns("stored-hash");
        var handler = Build(db, Firebase(student.FirebaseUid), binding, audit);

        var result = await handler.Handle(
            Command(rawDeviceToken: "good-token"), CancellationToken.None);

        result.DeviceTokenToSet.Should().Be("good-token"); // re-presented to slide the cookie
        result.Response.Student.BoundDevice!.Summary.Should().Be("iOS 18 · Safari");
        db.StudentDevices.DidNotReceive().Add(Arg.Any<StudentDevice>());
        await audit.DidNotReceive().WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentDeviceBound"), Arg.Any<CancellationToken>());
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentSignedIn"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_cookie_with_an_active_device_is_rejected_as_unrecognised()
    {
        var student = NewStudent();
        var device = ActiveDevice(student, hash: "stored-hash");
        var db = DbWith([student], [device]);
        var audit = Substitute.For<IAuditWriter>();
        var handler = Build(db, Firebase(student.FirebaseUid), Binding(), audit);

        var act = () => handler.Handle(Command(rawDeviceToken: null), CancellationToken.None).AsTask();

        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.Reason.Should().Be("device_not_recognized");
        db.StudentDevices.DidNotReceive().Add(Arg.Any<StudentDevice>());
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r =>
                r.Action == "StudentSignInRejected" && r.Summary.Contains("device_not_recognized")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Forged_cookie_is_rejected_as_unrecognised()
    {
        var student = NewStudent();
        var device = ActiveDevice(student, hash: "stored-hash");
        var db = DbWith([student], [device]);
        var audit = Substitute.For<IAuditWriter>();
        var binding = Binding();
        binding.Verify("forged").Returns((string?)null); // bad signature
        var handler = Build(db, Firebase(student.FirebaseUid), binding, audit);

        var act = () => handler.Handle(Command(rawDeviceToken: "forged"), CancellationToken.None).AsTask();

        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.Reason.Should().Be("device_not_recognized");
        await audit.Received(1).WriteAsync(
            Arg.Is<AuditWriteRequest>(r => r.Action == "StudentSignInRejected"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_different_devices_token_hash_is_rejected_as_unrecognised()
    {
        var student = NewStudent();
        var device = ActiveDevice(student, hash: "stored-hash");
        var db = DbWith([student], [device]);
        var audit = Substitute.For<IAuditWriter>();
        var binding = Binding();
        binding.Verify("other-device-token").Returns("a-different-hash"); // authentic token, but not THIS device
        var handler = Build(db, Firebase(student.FirebaseUid), binding, audit);

        var act = () => handler.Handle(
            Command(rawDeviceToken: "other-device-token"), CancellationToken.None).AsTask();

        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.Reason.Should().Be("device_not_recognized");
    }
}
