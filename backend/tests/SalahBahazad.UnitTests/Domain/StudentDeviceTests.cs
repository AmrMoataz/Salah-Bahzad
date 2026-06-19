using FluentAssertions;
using SalahBahazad.Domain.Events;
using StudentDeviceEntity = SalahBahazad.Domain.Entities.StudentDevice;

namespace SalahBahazad.UnitTests.Domain;

public class StudentDeviceTests
{
    private static StudentDeviceEntity NewBound() => StudentDeviceEntity.Bind(
        tenantId: Guid.NewGuid(),
        studentId: Guid.NewGuid(),
        deviceTokenHash: "hash",
        fingerprintSummary: "  iOS 18 · Safari ",
        now: DateTimeOffset.UtcNow);

    [Fact]
    public void Bind_creates_active_device_and_trims_fingerprint()
    {
        var device = NewBound();

        device.IsActive.Should().BeTrue();
        device.FingerprintSummary.Should().Be("iOS 18 · Safari");
        device.ClearedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Bind_throws_when_token_hash_blank()
    {
        var act = () => StudentDeviceEntity.Bind(
            Guid.NewGuid(), Guid.NewGuid(), "  ", null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Clear_deactivates_with_audit_fields_and_event()
    {
        var device = NewBound();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        device.Clear(actor, "  Lost phone  ", now);

        device.IsActive.Should().BeFalse();
        device.ClearedById.Should().Be(actor);
        device.ClearedAtUtc.Should().Be(now);
        device.ClearReason.Should().Be("Lost phone");
        device.DomainEvents.OfType<StudentDeviceClearedEvent>().Single().Reason.Should().Be("Lost phone");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Clear_requires_a_reason(string reason)
    {
        var device = NewBound();

        var act = () => device.Clear(Guid.NewGuid(), reason, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
        device.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Clear_throws_when_already_cleared()
    {
        var device = NewBound();
        device.Clear(Guid.NewGuid(), "first", DateTimeOffset.UtcNow);

        var act = () => device.Clear(Guid.NewGuid(), "second", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }
}
