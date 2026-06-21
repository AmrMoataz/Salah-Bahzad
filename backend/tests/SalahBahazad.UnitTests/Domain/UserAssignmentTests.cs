using FluentAssertions;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.UnitTests.Domain;

public class UserAssignmentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Question NewQuestion(int mark, string body = "x^2", string? hint = null) =>
        Question.Create(
            tenantId: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            bodyLatex: body,
            mark: mark,
            isValidForQuiz: true,
            hintUrl: hint,
            optionDrafts: [new QuestionOptionDraft("A", true), new QuestionOptionDraft("B", false)]);

    private static AssignmentQuestionForm BaseForm(Question q) =>
        new(q.BodyLatex, q.ImageObjectKey, [.. q.Options]);

    private static (UserAssignment Assignment, Guid Tenant, Guid Student) Generate(params Question[] questions)
    {
        var tenant = Guid.NewGuid();
        var student = Guid.NewGuid();
        var session = Session.Create(tenant, "S", null, 100m, 90, Guid.NewGuid(), Guid.NewGuid());
        var enrollment = Enrollment.Create(tenant, student, "Test Student", session, EnrollmentMethod.Code, null, 100m, Now);
        var assignment = UserAssignment.GenerateFor(tenant, enrollment, session, questions, BaseForm, Now);
        return (assignment, tenant, student);
    }

    [Fact]
    public void GenerateFor_snapshots_one_form_per_question_sums_marks_and_raises_event()
    {
        var (assignment, tenant, student) = Generate(NewQuestion(2), NewQuestion(3));

        assignment.TenantId.Should().Be(tenant);
        assignment.StudentId.Should().Be(student);
        assignment.Status.Should().Be(AssignmentStatus.InProgress);
        assignment.QuestionCount.Should().Be(2);
        assignment.MaxMarks.Should().Be(5);
        assignment.ScoreMarks.Should().BeNull();
        assignment.CorrectCount.Should().BeNull();
        assignment.TimeSpentSeconds.Should().Be(0);
        assignment.Questions.Select(q => q.Order).Should().Equal(1, 2);   // 1-based
        assignment.Questions.Should().OnlyContain(q => q.Options.Count == 2 && !q.IsAnswered);
        assignment.DomainEvents.OfType<AssignmentGeneratedEvent>().Should().ContainSingle()
            .Which.QuestionCount.Should().Be(2);
    }

    [Fact]
    public void GenerateFor_rejects_an_empty_question_set()
    {
        var act = () => Generate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Snapshot_is_independent_of_later_bank_edits()
    {
        var question = NewQuestion(2, body: "original");
        var (assignment, _, _) = Generate(question);
        var snapshot = assignment.Questions.Single();
        var originalTexts = snapshot.Options.Select(o => o.Text).OrderBy(t => t).ToList();

        // Mutate the bank wholesale after generation.
        question.Update(
            bodyLatex: "rewritten", mark: 9, isValidForQuiz: false, hintUrl: "h",
            optionDrafts: [new QuestionOptionDraft("C", false), new QuestionOptionDraft("D", true)]);

        snapshot.BodyLatex.Should().Be("original");
        snapshot.Mark.Should().Be(2);
        snapshot.Options.Select(o => o.Text).OrderBy(t => t).Should().Equal(originalTexts);  // still A/B
        snapshot.Options.Single(o => o.IsCorrect).Text.Should().Be("A");
    }

    [Fact]
    public void Answering_the_last_question_grades_marks_to_percent_and_completes()
    {
        var (assignment, _, _) = Generate(NewQuestion(2), NewQuestion(3));
        var q1 = assignment.Questions.First(q => q.Order == 1);
        var q2 = assignment.Questions.First(q => q.Order == 2);

        // Answer q1 correctly — still in progress until every question is answered.
        assignment.Answer(q1.Id, q1.Options.Single(o => o.IsCorrect).Id, Now);
        assignment.Status.Should().Be(AssignmentStatus.InProgress);
        assignment.AnsweredCount.Should().Be(1);

        // Answer q2 incorrectly — now complete; score = 2/5 marks ⇒ 40%.
        assignment.Answer(q2.Id, q2.Options.Single(o => !o.IsCorrect).Id, Now);
        assignment.Status.Should().Be(AssignmentStatus.Completed);
        assignment.CompletedAtUtc.Should().NotBeNull();
        assignment.ScoreMarks.Should().Be(2);
        assignment.CorrectCount.Should().Be(1);
        assignment.MaxMarks.Should().Be(5);
        assignment.Percent.Should().Be(40);

        var graded = assignment.DomainEvents.OfType<AssignmentGradedEvent>().Should().ContainSingle().Subject;
        graded.Percent.Should().Be(40);
        graded.ScoreMarks.Should().Be(2);
        graded.MaxMarks.Should().Be(5);
    }

    [Fact]
    public void Percent_rounds_half_away_from_zero()
    {
        // Three equal-mark questions, two correct ⇒ 2/3 ⇒ 66.67 ⇒ 67.
        var (assignment, _, _) = Generate(NewQuestion(1), NewQuestion(1), NewQuestion(1));
        var qs = assignment.Questions.OrderBy(q => q.Order).ToList();
        assignment.Answer(qs[0].Id, qs[0].Options.Single(o => o.IsCorrect).Id, Now);
        assignment.Answer(qs[1].Id, qs[1].Options.Single(o => o.IsCorrect).Id, Now);
        assignment.Answer(qs[2].Id, qs[2].Options.Single(o => !o.IsCorrect).Id, Now);

        assignment.Percent.Should().Be(67);
        assignment.ScoreMarks.Should().Be(2);
    }

    [Fact]
    public void Re_answering_before_completion_is_allowed()
    {
        var (assignment, _, _) = Generate(NewQuestion(2), NewQuestion(2));
        var q1 = assignment.Questions.First(q => q.Order == 1);

        assignment.Answer(q1.Id, q1.Options.Single(o => !o.IsCorrect).Id, Now); // wrong first
        assignment.Answer(q1.Id, q1.Options.Single(o => o.IsCorrect).Id, Now);  // changed to correct
        assignment.Status.Should().Be(AssignmentStatus.InProgress);
        q1.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public void Answering_after_completion_throws()
    {
        var (assignment, _, _) = Generate(NewQuestion(2));
        var q1 = assignment.Questions.Single();
        assignment.Answer(q1.Id, q1.Options.Single(o => o.IsCorrect).Id, Now);
        assignment.Status.Should().Be(AssignmentStatus.Completed);

        var act = () => assignment.Answer(q1.Id, q1.Options.First().Id, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Answering_an_option_from_another_question_throws()
    {
        var (assignment, _, _) = Generate(NewQuestion(2), NewQuestion(2));
        var q1 = assignment.Questions.First(q => q.Order == 1);
        var q2 = assignment.Questions.First(q => q.Order == 2);

        var act = () => assignment.Answer(q1.Id, q2.Options.First().Id, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Answering_an_unknown_question_throws()
    {
        var (assignment, _, _) = Generate(NewQuestion(2));
        var act = () => assignment.Answer(Guid.NewGuid(), assignment.Questions.Single().Options.First().Id, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddTime_accrues_and_ignores_non_positive()
    {
        var (assignment, _, _) = Generate(NewQuestion(2));
        assignment.AddTime(30);
        assignment.AddTime(15);
        assignment.AddTime(0);
        assignment.AddTime(-5);
        assignment.TimeSpentSeconds.Should().Be(45);
    }
}
