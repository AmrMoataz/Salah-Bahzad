using FluentAssertions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Profile.Commands.UpdateMyProfile;
using static SalahBahazad.UnitTests.Features.Staff.StaffTestHelpers;

namespace SalahBahazad.UnitTests.Features.Profile;

/// <summary>
/// Self-service profile update (FR-ADM-SET-001): the caller can only change their own display name,
/// resolved from the JWT identity — never a URL id.
/// </summary>
public class UpdateMyProfileHandlerTests
{
    [Fact]
    public async Task Updates_own_display_name()
    {
        var staff = NewStaff();
        var db = DbWith(staff);
        var handler = new UpdateMyProfileHandler(db, Actor(userId: staff.Id));

        var result = await handler.Handle(new UpdateMyProfileCommand("  Mostafa Kamel  "), CancellationToken.None);

        result.DisplayName.Should().Be("Mostafa Kamel");
        staff.DisplayName.Should().Be("Mostafa Kamel");
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_not_found_when_caller_has_no_staff_record()
    {
        var handler = new UpdateMyProfileHandler(DbWith(), Actor());

        var act = () => handler.Handle(new UpdateMyProfileCommand("New Name"), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
