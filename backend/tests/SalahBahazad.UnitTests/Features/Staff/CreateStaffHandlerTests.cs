using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Staff.Commands.CreateStaff;
using SalahBahazad.Domain.Enums;
using static SalahBahazad.UnitTests.Features.Staff.StaffTestHelpers;
using StaffEntity = SalahBahazad.Domain.Entities.Staff;

namespace SalahBahazad.UnitTests.Features.Staff;

public class CreateStaffHandlerTests
{
    [Fact]
    public async Task Creates_staff_provisions_firebase_and_normalises_email()
    {
        var firebase = Firebase("fb-new");
        var db = DbWith();
        var handler = new CreateStaffHandler(db, firebase, Actor(role: StaffRole.Teacher), NullLogger<CreateStaffHandler>.Instance);

        var result = await handler.Handle(
            new CreateStaffCommand("Hossam Fathy", "Hossam@Bahzad.edu", StaffRole.Assistant), CancellationToken.None);

        result.DisplayName.Should().Be("Hossam Fathy");
        result.Email.Should().Be("hossam@bahzad.edu");
        result.Role.Should().Be(StaffRole.Assistant);
        result.IsActive.Should().BeTrue();
        await firebase.Received(1).CreateUserAsync("hossam@bahzad.edu", "Hossam Fathy", Arg.Any<CancellationToken>());
        db.Staff.Received(1).Add(Arg.Is<StaffEntity>(s => s.FirebaseUid == "fb-new"));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_assigning_role_above_actor()
    {
        var firebase = Firebase();
        var handler = new CreateStaffHandler(
            DbWith(), firebase, Actor(role: StaffRole.Assistant), NullLogger<CreateStaffHandler>.Instance);

        var act = () => handler.Handle(
            new CreateStaffCommand("X", "x@x.com", StaffRole.Teacher), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ForbiddenException>();
        await firebase.DidNotReceive().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_duplicate_email()
    {
        var existing = NewStaff(email: "dupe@x.com");
        var handler = new CreateStaffHandler(
            DbWith(existing), Firebase(), Actor(role: StaffRole.Teacher), NullLogger<CreateStaffHandler>.Instance);

        var act = () => handler.Handle(
            new CreateStaffCommand("X", "Dupe@X.com", StaffRole.Assistant), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ConflictException>();
    }
}
