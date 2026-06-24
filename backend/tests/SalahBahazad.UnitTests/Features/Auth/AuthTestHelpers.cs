using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using NSubstitute;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;
using SalahBahazad.Application.Features.Auth.DTOs;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.UnitTests.Features.Auth;

/// <summary>
/// Shared setup for the student-exchange handler unit tests — a MockQueryable-backed
/// <see cref="IAppDbContext"/> (async LINQ, no real DB) whose <c>ExecuteInTransactionAsync</c> simply
/// invokes the supplied action, plus NSubstitute fakes for Firebase, the JWT issuer, the device-binding
/// service, and the audit writer.
/// </summary>
internal static class AuthTestHelpers
{
    public static IAppDbContext DbWith(
        IEnumerable<Student>? students = null,
        IEnumerable<StudentDevice>? devices = null)
    {
        // Build the mock sets first: calling BuildMockDbSet() inline inside .Returns(...) would configure a
        // nested substitute and clobber NSubstitute's "last call" state (CouldNotSetReturn...Exception).
        var studentSet = (students ?? []).ToList().BuildMockDbSet();
        var deviceSet = (devices ?? []).ToList().BuildMockDbSet();

        var db = Substitute.For<IAppDbContext>();
        db.Students.Returns(studentSet);
        db.StudentDevices.Returns(deviceSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Both student-exchange handlers wrap their success path in ExecuteInTransactionAsync; run the action
        // directly. The portal handler returns StudentExchangeResult, the app handler StudentAuthResponse —
        // the generic mock is configured per concrete TResult, so set up both instantiations.
        db.ExecuteInTransactionAsync(
                Arg.Any<Func<Task<StudentExchangeResult>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task<StudentExchangeResult>>>().Invoke());
        db.ExecuteInTransactionAsync(
                Arg.Any<Func<Task<StudentAuthResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task<StudentAuthResponse>>>().Invoke());

        return db;
    }

    public static IFirebaseAuthService Firebase(string uid)
    {
        var firebase = Substitute.For<IFirebaseAuthService>();
        firebase.VerifyIdTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FirebaseTokenClaims(uid, "s@example.com", "S", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)));
        return firebase;
    }

    public static IJwtTokenService Jwt()
    {
        var jwt = Substitute.For<IJwtTokenService>();
        // deviceId is Guid? — null on the device-agnostic app path (the token then carries no device_id).
        jwt.IssueStudentAccessToken(Arg.Any<Student>(), Arg.Any<Guid?>())
            .Returns(ci => new PlatformToken($"access-{Suffix(ci.Arg<Guid?>())}", DateTimeOffset.UtcNow.AddMinutes(15)));
        jwt.IssueStudentRefreshToken(Arg.Any<Student>(), Arg.Any<Guid?>())
            .Returns(ci => new PlatformToken($"refresh-{Suffix(ci.Arg<Guid?>())}", DateTimeOffset.UtcNow.AddDays(7)));
        return jwt;
    }

    private static string Suffix(Guid? deviceId) => deviceId is { } id ? id.ToString("N") : "app";

    /// <summary>A device-binding fake that issues a fixed (token, hash) and echoes the fingerprint.</summary>
    public static IDeviceBindingService Binding(
        string issuedRawToken = "new-raw-token", string issuedHash = "issued-hash")
    {
        var binding = Substitute.For<IDeviceBindingService>();
        binding.Issue(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((issuedRawToken, issuedHash));
        binding.Summarize(Arg.Any<string?>()).Returns(ci => ci.Arg<string?>());
        return binding;
    }

    public static Student NewStudent(
        StudentStatus status = StudentStatus.Active,
        string uid = "student-uid",
        string? rejectionReason = null,
        Guid? tenantId = null)
    {
        var student = Student.Register(
            tenantId ?? Guid.NewGuid(), uid, "Mariam Adel", "01099999999", "01000000000", null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Nile School", "v1", DateTimeOffset.UtcNow);

        switch (status)
        {
            case StudentStatus.Active:
                student.Approve();
                break;
            case StudentStatus.Rejected:
                student.Reject(rejectionReason ?? "Could not verify your ID.");
                break;
            case StudentStatus.Inactive:
                student.Approve();
                student.Deactivate();
                break;
        }

        return student;
    }

    public static StudentDevice ActiveDevice(Student student, string hash = "stored-hash", string? fingerprint = "iOS 18 · Safari")
        => StudentDevice.Bind(student.TenantId, student.Id, hash, fingerprint, DateTimeOffset.UtcNow);
}
