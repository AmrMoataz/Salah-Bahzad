using MockQueryable.NSubstitute;
using NSubstitute;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using StaffEntity = SalahBahazad.Domain.Entities.Staff;

namespace SalahBahazad.UnitTests.Features.Staff;

/// <summary>
/// Shared setup for staff handler unit tests — a MockQueryable-backed <see cref="IAppDbContext"/>
/// (async LINQ, no real DB / I/O) plus NSubstitute fakes for the resolvers and Firebase.
/// </summary>
internal static class StaffTestHelpers
{
    public static IAppDbContext DbWith(params StaffEntity[] seed)
    {
        var set = seed.ToList().BuildMockDbSet();
        var db = Substitute.For<IAppDbContext>();
        db.Staff.Returns(set);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return db;
    }

    public static ICurrentUserResolver Actor(
        Guid? userId = null, Guid? tenantId = null, StaffRole role = StaffRole.Teacher)
    {
        var user = Substitute.For<ICurrentUserResolver>();
        user.UserId.Returns(userId ?? Guid.NewGuid());
        user.TenantId.Returns(tenantId ?? Guid.NewGuid());
        user.Role.Returns(role);
        user.IsAuthenticated.Returns(true);
        return user;
    }

    public static IFirebaseAuthService Firebase(string uid = "fb-uid")
    {
        var firebase = Substitute.For<IFirebaseAuthService>();
        firebase.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(uid);
        firebase.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return firebase;
    }

    public static StaffEntity NewStaff(
        string email = "user@example.com",
        StaffRole role = StaffRole.Assistant,
        Guid? tenantId = null) =>
        StaffEntity.Create(tenantId ?? Guid.NewGuid(), "fb-" + Guid.NewGuid(), "Test User", email, role);
}
