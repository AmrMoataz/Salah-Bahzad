using System.Text.RegularExpressions;
using FluentAssertions;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.UnitTests.Domain;

public class CodeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static CodeBatch Mint(int quantity = 3, decimal value = 100m, ISet<string>? taken = null) =>
        CodeBatch.Generate(Tenant, Session, value, quantity, "CODES-20260620-01", taken ?? new HashSet<string>());

    [Fact]
    public void Generate_mints_quantity_active_codes_with_unique_serials_and_raises_event()
    {
        var batch = Mint(quantity: 5, value: 150m);

        batch.Quantity.Should().Be(5);
        batch.Value.Should().Be(150m);
        batch.Codes.Should().HaveCount(5);
        batch.Codes.Should().OnlyContain(c => c.Status == CodeStatus.Active);
        batch.Codes.Should().OnlyContain(c => c.SessionId == Session && c.Value == 150m && c.BatchId == batch.Id);
        batch.Codes.Select(c => c.Serial).Distinct().Should().HaveCount(5);
        batch.DomainEvents.OfType<CodeBatchGeneratedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Generate_serials_match_the_crockford_format()
    {
        var batch = Mint(quantity: 4);
        batch.Codes.Should().OnlyContain(c => Regex.IsMatch(c.Serial, "^SB-[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]{5}$"));
    }

    [Fact]
    public void Generate_does_not_reuse_an_already_taken_serial()
    {
        var batch = Mint(quantity: 3);
        var taken = batch.Codes.Select(c => c.Serial).ToHashSet();

        var second = Mint(quantity: 3, taken: taken);

        second.Codes.Select(c => c.Serial).Should().NotIntersectWith(batch.Codes.Select(c => c.Serial));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CodeBatch.MaxQuantity + 1)]
    public void Generate_rejects_quantity_out_of_range(int quantity)
    {
        var act = () => Mint(quantity: quantity);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Disable_then_enable_toggles_status_and_raises_events()
    {
        var code = Mint().Codes.First();

        code.Disable();
        code.Status.Should().Be(CodeStatus.Inactive);
        code.DomainEvents.OfType<CodeDisabledEvent>().Should().ContainSingle();

        code.Enable();
        code.Status.Should().Be(CodeStatus.Active);
        code.DomainEvents.OfType<CodeEnabledEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Used_code_cannot_be_disabled_enabled_or_deleted()
    {
        var code = Redeemed();

        code.Invoking(c => c.Disable()).Should().Throw<InvalidOperationException>();
        code.Invoking(c => c.Enable()).Should().Throw<InvalidOperationException>();
        code.Invoking(c => c.SoftDelete(Guid.NewGuid(), DateTimeOffset.UtcNow)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRedeemed_sets_used_with_join_and_raises_event()
    {
        var code = Mint().Codes.First();
        var student = Guid.NewGuid();
        var enrollment = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        code.MarkRedeemed(student, enrollment, now);

        code.Status.Should().Be(CodeStatus.Used);
        code.RedeemedByStudentId.Should().Be(student);
        code.RedeemedEnrollmentId.Should().Be(enrollment);
        code.RedeemedAtUtc.Should().Be(now);
        code.DomainEvents.OfType<CodeRedeemedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void A_disabled_code_cannot_be_redeemed()
    {
        var code = Mint().Codes.First();
        code.Disable();

        code.Invoking(c => c.MarkRedeemed(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReturnAfterRefund_reactivates_used_code_and_clears_join()
    {
        var code = Redeemed();

        code.ReturnAfterRefund();

        code.Status.Should().Be(CodeStatus.Active);
        code.RedeemedByStudentId.Should().BeNull();
        code.RedeemedEnrollmentId.Should().BeNull();
        code.RedeemedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ReturnAfterRefund_requires_a_used_code()
    {
        var code = Mint().Codes.First();
        code.Invoking(c => c.ReturnAfterRefund()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SoftDelete_sets_attribution_and_raises_event()
    {
        var code = Mint().Codes.First();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        code.SoftDelete(actor, now);

        code.IsDeleted.Should().BeTrue();
        code.DeletedById.Should().Be(actor);
        code.DeletedAtUtc.Should().Be(now);
        code.DomainEvents.OfType<CodeDeletedEvent>().Should().ContainSingle();
    }

    private static Code Redeemed()
    {
        var code = Mint().Codes.First();
        code.MarkRedeemed(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        return code;
    }
}
