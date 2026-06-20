using FluentAssertions;
using SalahBahazad.Application.Features.Quizzes;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.UnitTests.Features.Quizzes;

public class QuizQuestionSelectorTests
{
    private static Question Q(string body) =>
        Question.Create(
            tenantId: Guid.NewGuid(), sessionId: Guid.NewGuid(), bodyLatex: body, mark: 1,
            isValidForQuiz: true, hintUrl: null,
            optionDrafts: [new QuestionOptionDraft("A", true), new QuestionOptionDraft("B", false)]);

    private static List<Question> Bank(int n) => [.. Enumerable.Range(0, n).Select(i => Q($"q{i}"))];

    [Fact]
    public void Draw_returns_the_requested_count_of_distinct_questions()
    {
        var bank = Bank(10);

        var draw = QuizQuestionSelector.Draw(bank, count: 4, new Random(123));

        draw.Should().HaveCount(4);
        draw.Select(d => d.QuestionId).Should().OnlyHaveUniqueItems();
        draw.Select(d => d.QuestionId).Should().BeSubsetOf(bank.Select(q => q.Id));
        draw.Should().OnlyContain(d => d.Options.Count == 2);
    }

    [Fact]
    public void Draw_clamps_to_the_bank_size_when_it_is_thinner_than_the_count()
    {
        var bank = Bank(3);

        var draw = QuizQuestionSelector.Draw(bank, count: 5, new Random(1));

        draw.Should().HaveCount(3);
        draw.Select(d => d.QuestionId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Draw_is_independently_randomised_so_attempts_differ()
    {
        var bank = Bank(20);

        // Distinct random states (as successive attempts get) yield different subsets/orderings — the
        // property the proctored engine relies on (FR-PLAT-QZ-003). Same seed stays reproducible.
        var first = QuizQuestionSelector.Draw(bank, 6, new Random(1)).Select(d => d.QuestionId).ToList();
        var second = QuizQuestionSelector.Draw(bank, 6, new Random(2)).Select(d => d.QuestionId).ToList();
        var firstAgain = QuizQuestionSelector.Draw(bank, 6, new Random(1)).Select(d => d.QuestionId).ToList();

        first.Should().NotEqual(second);
        first.Should().Equal(firstAgain); // deterministic for a fixed seed
    }

    [Fact]
    public void Draw_can_render_a_variation_form_copying_its_options()
    {
        var question = Q("base");
        question.AddVariation(
            "variation-body",
            [new QuestionOptionDraft("X", false), new QuestionOptionDraft("Y", true)]);

        // Probe many random states; with one base + one variation the variation form must appear.
        var forms = Enumerable.Range(0, 50)
            .Select(seed => QuizQuestionSelector.Draw([question], 1, new Random(seed)).Single())
            .ToList();

        forms.Should().Contain(f => f.BodyLatex == "variation-body");
        forms.Should().Contain(f => f.BodyLatex == "base");
        forms.Where(f => f.BodyLatex == "variation-body").Should()
            .OnlyContain(f => f.Options.Single(o => o.IsCorrect).Text == "Y");
    }
}
