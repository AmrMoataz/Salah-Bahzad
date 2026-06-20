using FluentAssertions;
using SalahBahazad.Application.Features.Audit;

namespace SalahBahazad.UnitTests.Features.Audit;

/// <summary>The backend-owned action→category map + sensitive set (contract §1/§4/§5).</summary>
public class AuditActionCategoryTests
{
    [Theory]
    [InlineData("StudentApproved", "approval")]
    [InlineData("StudentRejected", "approval")]
    [InlineData("CodeBatchGenerated", "code")]
    [InlineData("CodeRedeemed", "code")]
    [InlineData("CodesExported", "code")]
    [InlineData("EnrollmentRefunded", "enrollment")]
    [InlineData("EnrollmentCreated", "enrollment")]
    [InlineData("SessionPublished", "session")]
    [InlineData("SessionVideoAdded", "session")]
    [InlineData("QuestionAdded", "question")]
    [InlineData("StudentDeviceCleared", "device")]
    [InlineData("StudentRegistered", "student")]
    [InlineData("StudentIdImageViewed", "student")]
    public void CategoryOf_maps_known_actions(string action, string expected) =>
        AuditActionCategory.CategoryOf(action).Should().Be(expected);

    [Theory]
    [InlineData("Created")]
    [InlineData("Updated")]
    [InlineData("Deleted")]
    [InlineData("SomethingBrandNew")]
    [InlineData(null)]
    public void CategoryOf_falls_back_to_other_for_generic_or_unmapped(string? action) =>
        AuditActionCategory.CategoryOf(action).Should().Be("other");

    [Fact]
    public void CategoryOf_catches_future_staff_actions_by_prefix() =>
        AuditActionCategory.CategoryOf("StaffCreated").Should().Be("staff");

    [Fact]
    public void ActionsInCategory_returns_the_member_actions()
    {
        AuditActionCategory.ActionsInCategory("approval")
            .Should().BeEquivalentTo("StudentApproved", "StudentRejected");
        AuditActionCategory.ActionsInCategory("CODE") // case-insensitive
            .Should().Contain("CodeRedeemed").And.Contain("CodesExported");
        AuditActionCategory.ActionsInCategory("unknown").Should().BeEmpty();
    }

    [Fact]
    public void SensitiveAuditActions_contains_only_the_id_image_read()
    {
        SensitiveAuditActions.Contains("StudentIdImageViewed").Should().BeTrue();
        SensitiveAuditActions.Contains("StudentApproved").Should().BeFalse();
        SensitiveAuditActions.Contains(null).Should().BeFalse();
    }
}
