using FluentAssertions;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;
using SessionEntity = SalahBahazad.Domain.Entities.Session;

namespace SalahBahazad.UnitTests.Domain;

public class EnrollmentTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static SessionEntity SessionWithVideos(int validityDays = 90, params int[] accessCounts)
    {
        var session = SessionEntity.Create(Tenant, "Algebra", null, 100m, validityDays, Guid.NewGuid(), Guid.NewGuid());
        for (var i = 0; i < accessCounts.Length; i++)
            session.AddVideo($"v{i}", accessCounts[i], $"k{i}");
        return session;
    }

    [Fact]
    public void Create_provisions_one_counter_per_video_and_records_payment_and_event()
    {
        var session = SessionWithVideos(90, 3, 2);
        var student = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var enrollment = Enrollment.Create(Tenant, student, session, EnrollmentMethod.Code, Guid.NewGuid(), 100m, now);

        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.Method.Should().Be(EnrollmentMethod.Code);
        enrollment.Amount.Should().Be(100m);

        enrollment.VideoAccesses.Should().HaveCount(2);
        enrollment.VideoAccesses.Should().OnlyContain(a => a.AccessRemaining == a.AccessAllowed);
        enrollment.VideoAccesses.Select(a => a.AccessAllowed).Should().BeEquivalentTo(new[] { 3, 2 });

        var payment = enrollment.Payments.Should().ContainSingle().Subject;
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.Method.Should().Be(PaymentMethod.CodeRedemption);
        payment.Amount.Should().Be(100m);

        enrollment.DomainEvents.OfType<EnrollmentCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_with_validity_days_sets_expiry()
    {
        var now = DateTimeOffset.UtcNow;
        var enrollment = Enrollment.Create(
            Tenant, Guid.NewGuid(), SessionWithVideos(30), EnrollmentMethod.Unlock, null, 0m, now);

        enrollment.ExpiresAtUtc.Should().Be(now.AddDays(30));
    }

    [Fact]
    public void Create_with_zero_validity_has_no_expiry()
    {
        var enrollment = Enrollment.Create(
            Tenant, Guid.NewGuid(), SessionWithVideos(0), EnrollmentMethod.Unlock, null, 0m, DateTimeOffset.UtcNow);

        enrollment.ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public void Unlock_enrollment_records_a_zero_amount_unlock_payment()
    {
        var enrollment = Enrollment.Create(
            Tenant, Guid.NewGuid(), SessionWithVideos(30, 1), EnrollmentMethod.Unlock, null, 0m, DateTimeOffset.UtcNow);

        var payment = enrollment.Payments.Should().ContainSingle().Subject;
        payment.Method.Should().Be(PaymentMethod.Unlock);
        payment.Amount.Should().Be(0m);
        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public void Refund_flips_to_refunded_adds_reversal_and_raises_event()
    {
        var enrollment = Enrollment.Create(
            Tenant, Guid.NewGuid(), SessionWithVideos(30, 1), EnrollmentMethod.Code, Guid.NewGuid(), 100m,
            DateTimeOffset.UtcNow);

        enrollment.Refund(DateTimeOffset.UtcNow, "SB-ABCDE-12345");

        enrollment.Status.Should().Be(EnrollmentStatus.Refunded);
        enrollment.Payments.Should().HaveCount(2);
        enrollment.Payments.Should().ContainSingle(p => p.Status == PaymentStatus.Refunded);
        enrollment.DomainEvents.OfType<EnrollmentRefundedEvent>().Should().ContainSingle()
            .Which.ReturnedCodeSerial.Should().Be("SB-ABCDE-12345");
    }

    [Fact]
    public void Refund_requires_an_active_enrollment()
    {
        var enrollment = Enrollment.Create(
            Tenant, Guid.NewGuid(), SessionWithVideos(30, 1), EnrollmentMethod.Code, Guid.NewGuid(), 100m,
            DateTimeOffset.UtcNow);
        enrollment.Refund(DateTimeOffset.UtcNow, null);

        enrollment.Invoking(e => e.Refund(DateTimeOffset.UtcNow, null)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Extend_resets_counters_pushes_expiry_and_does_not_duplicate_rows()
    {
        var session = SessionWithVideos(30, 3);
        var firstVideoId = session.Videos.First().Id;
        var student = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;

        var enrollment = Enrollment.Create(Tenant, student, session, EnrollmentMethod.Code, Guid.NewGuid(), 100m, start);
        enrollment.Refund(start, null); // make it non-active so it can be extended/reused

        // The video's budget is raised and a new video is added before re-enrolling.
        session.UpdateVideo(firstVideoId, "v0", 5, newSourceObjectKey: null);
        session.AddVideo("v-new", 4, "k-new");

        var later = start.AddDays(10);
        enrollment.Extend(session, EnrollmentMethod.Code, Guid.NewGuid(), 120m, later);

        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.ExpiresAtUtc.Should().Be(later.AddDays(30));            // pushed forward from the new "now"
        enrollment.VideoAccesses.Should().HaveCount(2);                    // one per video — NOT duplicated
        enrollment.VideoAccesses.Single(a => a.VideoId == firstVideoId).AccessRemaining.Should().Be(5); // reset to new budget
        enrollment.DomainEvents.OfType<EnrollmentExtendedEvent>().Should().ContainSingle();
    }
}
