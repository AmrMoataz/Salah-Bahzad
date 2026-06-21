using FluentAssertions;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SessionEntity = SalahBahazad.Domain.Entities.Session;

namespace SalahBahazad.UnitTests.Domain;

public class EnrollmentVideoAccessTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static EnrollmentVideoAccess AccessWith(int accessCount)
    {
        var session = SessionEntity.Create(Tenant, "Algebra", null, 100m, 90, Guid.NewGuid(), Guid.NewGuid());
        session.AddVideo("v", accessCount, "sessions/t/s/videos/a.mp4");
        var enrollment = Enrollment.Create(
            Tenant, Guid.NewGuid(), session, EnrollmentMethod.Code, Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
        return enrollment.VideoAccesses.Single();
    }

    [Fact]
    public void Decrement_reduces_remaining_and_leaves_allowed_unchanged()
    {
        var access = AccessWith(3);

        access.Decrement();

        access.AccessRemaining.Should().Be(2);
        access.AccessAllowed.Should().Be(3); // FR-PLAT-VID-002: the budget granted is immutable
    }

    [Fact]
    public void Decrement_at_zero_throws()
    {
        var access = AccessWith(1);
        access.Decrement();
        access.AccessRemaining.Should().Be(0);

        access.Invoking(a => a.Decrement()).Should().Throw<InvalidOperationException>();
    }
}
