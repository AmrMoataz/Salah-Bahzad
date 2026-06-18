using FluentAssertions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Staff.Commands.UpdateStaff;
using SalahBahazad.Domain.Enums;
using static SalahBahazad.UnitTests.Features.Staff.StaffTestHelpers;

namespace SalahBahazad.UnitTests.Features.Staff;

public class UpdateStaffHandlerTests
{
    [Fact]
    public async Task Updates_details_and_normalises_email()
    {
        var existing = NewStaff(email: "old@x.com", role: StaffRole.Assistant);
        var db = DbWith(existing);
        var handler = new UpdateStaffHandler(db, Actor(role: StaffRole.Teacher));

        var result = await handler.Handle(
            new UpdateStaffCommand(existing.Id, "New Name", "New@X.com", StaffRole.Assistant), CancellationToken.None);

        result.DisplayName.Should().Be("New Name");
        result.Email.Should().Be("new@x.com");
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_NotFound_when_missing()
    {
        var handler = new UpdateStaffHandler(DbWith(), Actor(role: StaffRole.Teacher));

        var act = () => handler.Handle(
            new UpdateStaffCommand(Guid.NewGuid(), "N", "n@x.com", StaffRole.Assistant), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Rejects_role_above_actor()
    {
        var existing = NewStaff();
        var handler = new UpdateStaffHandler(DbWith(existing), Actor(role: StaffRole.Assistant));

        var act = () => handler.Handle(
            new UpdateStaffCommand(existing.Id, "N", "n@x.com", StaffRole.Teacher), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Rejects_email_taken_by_another()
    {
        var a = NewStaff(email: "a@x.com");
        var b = NewStaff(email: "b@x.com");
        var handler = new UpdateStaffHandler(DbWith(a, b), Actor(role: StaffRole.Teacher));

        var act = () => handler.Handle(
            new UpdateStaffCommand(a.Id, "A", "b@x.com", StaffRole.Assistant), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ConflictException>();
    }
}
