using FluentAssertions;
using SalahBahazad.Domain.Enums;
using StaffEntity = SalahBahazad.Domain.Entities.Staff;

namespace SalahBahazad.UnitTests.Domain;

public class StaffTests
{
    [Fact]
    public void Create_trims_name_and_lowercases_email()
    {
        var staff = StaffEntity.Create(Guid.NewGuid(), "fb-uid", "  Jane Doe  ", "Jane.DOE@Example.com", StaffRole.Assistant);

        staff.DisplayName.Should().Be("Jane Doe");
        staff.Email.Should().Be("jane.doe@example.com");
        staff.Role.Should().Be(StaffRole.Assistant);
        staff.IsActive.Should().BeTrue();
        staff.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_throws_when_displayName_blank(string displayName)
    {
        var act = () => StaffEntity.Create(Guid.NewGuid(), "fb-uid", displayName, "a@x.com", StaffRole.Assistant);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateRole_throws_when_assigning_above_actor()
    {
        var staff = StaffEntity.Create(Guid.NewGuid(), "fb", "Asst", "asst@x.com", StaffRole.Assistant);

        // An Assistant actor cannot elevate anyone to Teacher (FR-PLAT-ROLE-002).
        var act = () => staff.UpdateRole(StaffRole.Teacher, actorRole: StaffRole.Assistant);

        act.Should().Throw<InvalidOperationException>();
        staff.Role.Should().Be(StaffRole.Assistant);
    }

    [Theory]
    [InlineData(StaffRole.Assistant)]
    [InlineData(StaffRole.Teacher)]
    public void UpdateRole_allows_role_at_or_below_actor(StaffRole newRole)
    {
        var staff = StaffEntity.Create(Guid.NewGuid(), "fb", "User", "u@x.com", StaffRole.Assistant);

        staff.UpdateRole(newRole, actorRole: StaffRole.Teacher);

        staff.Role.Should().Be(newRole);
    }

    [Fact]
    public void Deactivate_then_Activate_toggles_IsActive()
    {
        var staff = StaffEntity.Create(Guid.NewGuid(), "fb", "User", "u@x.com", StaffRole.Assistant);

        staff.Deactivate();
        staff.IsActive.Should().BeFalse();

        staff.Activate();
        staff.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecordSignIn_stamps_last_seen()
    {
        var staff = StaffEntity.Create(Guid.NewGuid(), "fb", "User", "u@x.com", StaffRole.Assistant);
        staff.LastSeenAtUtc.Should().BeNull();
        var now = DateTimeOffset.UtcNow;

        staff.RecordSignIn(now);

        staff.LastSeenAtUtc.Should().Be(now);
    }

    [Fact]
    public void SoftDelete_sets_audit_attribution()
    {
        var staff = StaffEntity.Create(Guid.NewGuid(), "fb", "User", "u@x.com", StaffRole.Assistant);
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        staff.SoftDelete(actor, now);

        staff.IsDeleted.Should().BeTrue();
        staff.DeletedById.Should().Be(actor);
        staff.DeletedAtUtc.Should().Be(now);
    }
}
