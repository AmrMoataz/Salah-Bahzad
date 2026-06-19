using FluentAssertions;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.UnitTests.Domain;

public class QuestionTests
{
    private static IReadOnlyList<QuestionOptionDraft> TwoOptions(int correctIndex = 0) =>
        [new QuestionOptionDraft("A", correctIndex == 0), new QuestionOptionDraft("B", correctIndex == 1)];

    private static Question NewQuestion(string? body = "  x^2  ") => Question.Create(
        tenantId: Guid.NewGuid(),
        sessionId: Guid.NewGuid(),
        bodyLatex: body,
        mark: 2,
        isValidForQuiz: true,
        hintUrl: "  https://youtu.be/abc  ",
        optionDrafts: TwoOptions());

    [Fact]
    public void Create_valid_question_trims_and_orders_options()
    {
        var q = NewQuestion();

        q.BodyLatex.Should().Be("x^2");
        q.HintUrl.Should().Be("https://youtu.be/abc");
        q.Mark.Should().Be(2);
        q.IsValidForQuiz.Should().BeTrue();
        q.ImageObjectKey.Should().BeNull();
        q.Options.Should().HaveCount(2);
        q.Options.Select(o => o.Order).Should().Equal(0, 1);
        q.Options.Count(o => o.IsCorrect).Should().Be(1);
        q.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_requires_body_when_no_image()
    {
        var act = () => Question.Create(
            Guid.NewGuid(), Guid.NewGuid(), bodyLatex: null, mark: 1, isValidForQuiz: true,
            hintUrl: null, TwoOptions());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_requires_at_least_two_options()
    {
        var act = () => Question.Create(
            Guid.NewGuid(), Guid.NewGuid(), "b", 1, true, null, [new QuestionOptionDraft("A", true)]);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false, false)] // none correct
    [InlineData(true, true)]   // two correct
    public void Create_requires_exactly_one_correct_option(bool a, bool b)
    {
        var act = () => Question.Create(
            Guid.NewGuid(), Guid.NewGuid(), "b", 1, true, null,
            [new QuestionOptionDraft("A", a), new QuestionOptionDraft("B", b)]);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_rejects_non_positive_mark()
    {
        var act = () => Question.Create(Guid.NewGuid(), Guid.NewGuid(), "b", 0, true, null, TwoOptions());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_can_clear_body_when_image_present()
    {
        var q = NewQuestion();
        q.SetImage("questions/t/images/x.png");

        q.Update(bodyLatex: null, mark: 3, isValidForQuiz: false, hintUrl: null, TwoOptions(1));

        q.BodyLatex.Should().BeNull();
        q.Mark.Should().Be(3);
        q.IsValidForQuiz.Should().BeFalse();
        q.Options.Single(o => o.IsCorrect).Text.Should().Be("B");
    }

    [Fact]
    public void Update_without_body_or_image_is_rejected()
    {
        var q = NewQuestion();
        var act = () => q.Update(bodyLatex: " ", mark: 1, isValidForQuiz: true, hintUrl: null, TwoOptions());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ClearImage_throws_when_it_would_leave_no_content()
    {
        var q = NewQuestion();
        q.SetImage("questions/t/images/x.png");
        q.Update(bodyLatex: null, mark: 2, isValidForQuiz: true, hintUrl: null, TwoOptions()); // image-only now

        var act = () => q.ClearImage();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddVariation_requires_body_and_valid_options()
    {
        var q = NewQuestion();

        var variation = q.AddVariation("  y=mx+b  ", TwoOptions(1));
        q.Variations.Should().ContainSingle();
        variation.BodyLatex.Should().Be("y=mx+b");
        variation.Options.Should().HaveCount(2);

        var noBody = () => q.AddVariation(null, TwoOptions());
        noBody.Should().Throw<InvalidOperationException>();

        var badOptions = () => q.AddVariation("b", [new QuestionOptionDraft("only", true)]);
        badOptions.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateVariation_and_RemoveVariation_work()
    {
        var q = NewQuestion();
        var variation = q.AddVariation("v1", TwoOptions());

        q.UpdateVariation(variation.Id, "v1-edited", TwoOptions(1));
        variation.BodyLatex.Should().Be("v1-edited");
        variation.Options.Single(o => o.IsCorrect).Text.Should().Be("B");

        q.RemoveVariation(variation.Id);
        q.Variations.Should().BeEmpty();
    }

    [Fact]
    public void SetVariationImage_allows_clearing_variation_body_on_update()
    {
        var q = NewQuestion();
        var variation = q.AddVariation("v1", TwoOptions());

        q.SetVariationImage(variation.Id, "questions/t/images/v.png");
        q.UpdateVariation(variation.Id, bodyLatex: null, TwoOptions()); // image present → allowed

        variation.BodyLatex.Should().BeNull();
        variation.ImageObjectKey.Should().Be("questions/t/images/v.png");
    }

    [Fact]
    public void RemoveVariation_unknown_throws()
    {
        var q = NewQuestion();
        var act = () => q.RemoveVariation(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SoftDelete_sets_attribution()
    {
        var q = NewQuestion();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        q.SoftDelete(actor, now);

        q.IsDeleted.Should().BeTrue();
        q.DeletedById.Should().Be(actor);
        q.DeletedAtUtc.Should().Be(now);
    }
}
