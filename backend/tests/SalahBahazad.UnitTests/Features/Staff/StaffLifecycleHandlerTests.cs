using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Staff.Commands.DeleteStaff;
using SalahBahazad.Application.Features.Staff.Commands.SendStaffPasswordReset;
using SalahBahazad.Application.Features.Staff.Commands.SetStaffActive;
using static SalahBahazad.UnitTests.Features.Staff.StaffTestHelpers;

namespace SalahBahazad.UnitTests.Features.Staff;

public class StaffLifecycleHandlerTests
{
    // ── SetStaffActive ────────────────────────────────────────────────
    [Fact]
    public async Task Deactivate_updates_state_and_disables_firebase()
    {
        var staff = NewStaff();
        var firebase = Firebase();
        var handler = new SetStaffActiveHandler(DbWith(staff), firebase, Actor());

        var result = await handler.Handle(new SetStaffActiveCommand(staff.Id, false), CancellationToken.None);

        result.IsActive.Should().BeFalse();
        staff.IsActive.Should().BeFalse();
        await firebase.Received(1).SetUserDisabledAsync(staff.FirebaseUid, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cannot_deactivate_self()
    {
        var staff = NewStaff();
        var handler = new SetStaffActiveHandler(DbWith(staff), Firebase(), Actor(userId: staff.Id));

        var act = () => handler.Handle(new SetStaffActiveCommand(staff.Id, false), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task SetActive_throws_NotFound_when_missing()
    {
        var handler = new SetStaffActiveHandler(DbWith(), Firebase(), Actor());

        var act = () => handler.Handle(new SetStaffActiveCommand(Guid.NewGuid(), true), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── DeleteStaff ───────────────────────────────────────────────────
    [Fact]
    public async Task Delete_soft_deletes_and_disables_firebase()
    {
        var staff = NewStaff();
        var firebase = Firebase();
        var actorId = Guid.NewGuid();
        var handler = new DeleteStaffHandler(
            DbWith(staff), TimeProvider.System, firebase, Actor(userId: actorId), NullLogger<DeleteStaffHandler>.Instance);

        await handler.Handle(new DeleteStaffCommand(staff.Id), CancellationToken.None);

        staff.IsDeleted.Should().BeTrue();
        staff.DeletedById.Should().Be(actorId);
        await firebase.Received(1).SetUserDisabledAsync(staff.FirebaseUid, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cannot_delete_self()
    {
        var staff = NewStaff();
        var handler = new DeleteStaffHandler(
            DbWith(staff), TimeProvider.System, Firebase(), Actor(userId: staff.Id), NullLogger<DeleteStaffHandler>.Instance);

        var act = () => handler.Handle(new DeleteStaffCommand(staff.Id), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── SendStaffPasswordReset ────────────────────────────────────────
    [Fact]
    public async Task PasswordReset_asks_firebase_to_email_the_staff_address()
    {
        var staff = NewStaff(email: "reset@x.com");
        var firebase = Firebase();
        var handler = new SendStaffPasswordResetHandler(
            DbWith(staff), firebase, Actor(), NullLogger<SendStaffPasswordResetHandler>.Instance);

        var result = await handler.Handle(new SendStaffPasswordResetCommand(staff.Id), CancellationToken.None);

        result.Email.Should().Be("reset@x.com");
        await firebase.Received(1).SendPasswordResetEmailAsync("reset@x.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PasswordReset_throws_NotFound_when_missing()
    {
        var handler = new SendStaffPasswordResetHandler(
            DbWith(), Firebase(), Actor(), NullLogger<SendStaffPasswordResetHandler>.Instance);

        var act = () => handler.Handle(new SendStaffPasswordResetCommand(Guid.NewGuid()), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
