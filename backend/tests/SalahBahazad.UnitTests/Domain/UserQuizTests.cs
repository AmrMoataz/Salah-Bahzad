using FluentAssertions;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.UnitTests.Domain;

public class UserQuizTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static QuizSetting Setting(int time = 30, int count = 2, int attempts = 3, int pass = 60)
        => QuizSetting.Create(time, count, attempts, pass);

    private static Question Q(int mark = 1, string body = "x^2") =>
        Question.Create(
            tenantId: Guid.NewGuid(), sessionId: Guid.NewGuid(), bodyLatex: body, mark: mark,
            isValidForQuiz: true, hintUrl: null,
            optionDrafts: [new QuestionOptionDraft("A", true), new QuestionOptionDraft("B", false)]);

    private static IReadOnlyList<QuizQuestionForm> Draws(params Question[] qs) =>
        [.. qs.Select(q => new QuizQuestionForm(q.Id, q.BodyLatex, q.ImageObjectKey, q.Mark, [.. q.Options]))];

    private static UserQuiz Generate(QuizSetting setting, out Guid tenant, out Guid student)
    {
        tenant = Guid.NewGuid();
        student = Guid.NewGuid();
        var grade = Guid.NewGuid();
        var spec = Guid.NewGuid();
        var sourceA = Session.Create(tenant, "A", null, 100m, 90, grade, spec);
        var gatedB = Session.Create(tenant, "B", null, 100m, 90, grade, spec);
        var enrollment = Enrollment.Create(tenant, student, gatedB, EnrollmentMethod.Code, null, 100m, Now);
        return UserQuiz.GenerateFor(tenant, enrollment, gatedB, sourceA, setting, Now);
    }

    private static UserQuiz Generate(QuizSetting setting) => Generate(setting, out _, out _);

    /// <summary>Answers every question of an attempt; the first <paramref name="correctCount"/> correctly.</summary>
    private static void AnswerAttempt(UserQuiz quiz, QuizAttempt attempt, int correctCount)
    {
        var ordered = attempt.Questions.OrderBy(q => q.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var q = ordered[i];
            var option = i < correctCount
                ? q.Options.Single(o => o.IsCorrect)
                : q.Options.Single(o => !o.IsCorrect);
            quiz.Answer(attempt.Id, q.Id, option.Id, Now);
        }
    }

    [Fact]
    public void GenerateFor_snapshots_the_settings_and_raises_a_system_generation_event()
    {
        var quiz = Generate(Setting(time: 20, count: 5, attempts: 3, pass: 60), out var tenant, out var student);

        quiz.TenantId.Should().Be(tenant);
        quiz.StudentId.Should().Be(student);
        quiz.TimeLimitMinutes.Should().Be(20);
        quiz.QuestionCount.Should().Be(5);
        quiz.AttemptCount.Should().Be(3);
        quiz.MinPassPercent.Should().Be(60);
        quiz.AttemptsUsed.Should().Be(0);
        quiz.AttemptsRemaining.Should().Be(3);
        quiz.BestPercent.Should().BeNull();
        quiz.Passed.Should().BeFalse();
        quiz.ActiveAttempt.Should().BeNull();
        quiz.DomainEvents.OfType<QuizGeneratedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void StartAttempt_snapshots_draws_sets_deadline_and_consumes_an_attempt()
    {
        var quiz = Generate(Setting(time: 30, attempts: 3));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);

        attempt.Number.Should().Be(1);
        attempt.Status.Should().Be(QuizAttemptStatus.InProgress);
        attempt.DeadlineUtc.Should().Be(Now.AddMinutes(30));
        attempt.Questions.Select(q => q.Order).Should().Equal(1, 2); // 1-based
        attempt.Questions.Should().OnlyContain(q => q.Options.Count == 2 && !q.IsAnswered);
        quiz.AttemptsUsed.Should().Be(1);
        quiz.AttemptsRemaining.Should().Be(2);
        quiz.ActiveAttempt.Should().Be(attempt);
        quiz.DomainEvents.OfType<QuizAttemptStartedEvent>().Should().ContainSingle()
            .Which.Number.Should().Be(1);
    }

    [Fact]
    public void StartAttempt_while_one_is_active_throws()
    {
        var quiz = Generate(Setting(attempts: 3));
        quiz.StartAttempt(Draws(Q(), Q()), Now);

        var act = () => quiz.StartAttempt(Draws(Q(), Q()), Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already in progress*");
    }

    [Fact]
    public void Submitting_grades_marks_to_percent_and_raises_submitted_plus_graded()
    {
        var quiz = Generate(Setting(count: 2, pass: 60));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, attempt, correctCount: 1); // 1/2 ⇒ 50%

        quiz.SubmitAttempt(attempt.Id, Now);

        attempt.Status.Should().Be(QuizAttemptStatus.Submitted);
        attempt.ScorePercent.Should().Be(50);
        quiz.BestPercent.Should().Be(50);
        quiz.Passed.Should().BeFalse(); // 50 < 60
        quiz.DomainEvents.OfType<QuizAttemptSubmittedEvent>().Should().ContainSingle()
            .Which.ScorePercent.Should().Be(50);
        quiz.DomainEvents.OfType<QuizGradedEvent>().Should().ContainSingle()
            .Which.BestPercent.Should().Be(50);
    }

    [Fact]
    public void Pass_is_inclusive_at_exactly_the_minimum_the_ge_fix() // FR-PLAT-QZ-008 (#7)
    {
        var quiz = Generate(Setting(count: 2, pass: 50));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, attempt, correctCount: 1); // exactly 50%

        quiz.SubmitAttempt(attempt.Id, Now);

        quiz.BestPercent.Should().Be(50);
        quiz.Passed.Should().BeTrue(); // 50 >= 50 — would be false under the old '>' bug
        quiz.DomainEvents.OfType<QuizGradedEvent>().Last().Passed.Should().BeTrue();
    }

    [Fact]
    public void BestPercent_is_the_max_across_attempts_in_either_order()
    {
        var quiz = Generate(Setting(count: 2, attempts: 3, pass: 90));

        var a1 = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, a1, correctCount: 2); // 100%
        quiz.SubmitAttempt(a1.Id, Now);

        var a2 = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, a2, correctCount: 1); // 50%
        quiz.SubmitAttempt(a2.Id, Now);

        quiz.BestPercent.Should().Be(100); // the lower later attempt never lowers the best
        quiz.Passed.Should().BeTrue();
        quiz.AttemptsUsed.Should().Be(2);
    }

    [Fact]
    public void Attempts_exhausted_blocks_a_further_start()
    {
        var quiz = Generate(Setting(count: 2, attempts: 1));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, attempt, correctCount: 2);
        quiz.SubmitAttempt(attempt.Id, Now);

        quiz.AttemptsRemaining.Should().Be(0);
        var act = () => quiz.StartAttempt(Draws(Q(), Q()), Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*No quiz attempts remain*");
    }

    [Fact]
    public void Answering_a_terminal_attempt_throws()
    {
        var quiz = Generate(Setting(count: 1, attempts: 2));
        var attempt = quiz.StartAttempt(Draws(Q()), Now);
        var q = attempt.Questions.Single();
        quiz.SubmitAttempt(attempt.Id, Now);

        var act = () => quiz.Answer(attempt.Id, q.Id, q.Options.First().Id, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*no longer in progress*");
    }

    [Fact]
    public void Answering_after_the_deadline_throws()
    {
        var quiz = Generate(Setting(time: 30, count: 1));
        var attempt = quiz.StartAttempt(Draws(Q()), Now);
        var q = attempt.Questions.Single();

        var act = () => quiz.Answer(attempt.Id, q.Id, q.Options.First().Id, Now.AddMinutes(31));
        act.Should().Throw<InvalidOperationException>().WithMessage("*time limit has elapsed*");
    }

    [Fact]
    public void Forfeiting_scores_zero_consumes_the_attempt_and_raises_no_grade_event()
    {
        var quiz = Generate(Setting(count: 2, attempts: 3, pass: 60));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, attempt, correctCount: 2); // would be 100% if submitted

        var forfeited = quiz.ForfeitActiveAttempt(Now);

        forfeited.Should().Be(attempt);
        attempt.Status.Should().Be(QuizAttemptStatus.Forfeited);
        attempt.ScorePercent.Should().Be(0);
        quiz.AttemptsUsed.Should().Be(1); // consumed
        quiz.BestPercent.Should().Be(0);
        quiz.Passed.Should().BeFalse();
        quiz.DomainEvents.OfType<QuizAttemptForfeitedEvent>().Should().ContainSingle();
        quiz.DomainEvents.OfType<QuizGradedEvent>().Should().BeEmpty(); // a 0 never improves best-of
    }

    [Fact]
    public void Forfeit_with_no_active_attempt_is_a_no_op()
    {
        var quiz = Generate(Setting(count: 1, attempts: 2));
        var attempt = quiz.StartAttempt(Draws(Q()), Now);
        quiz.SubmitAttempt(attempt.Id, Now);

        quiz.ForfeitActiveAttempt(Now).Should().BeNull(); // a disconnect after a clean submit changes nothing
        attempt.Status.Should().Be(QuizAttemptStatus.Submitted);
    }

    [Fact]
    public void TimeOut_grades_answered_marks_the_full_window_and_is_system_attributed()
    {
        var quiz = Generate(Setting(time: 10, count: 2, pass: 60));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, attempt, correctCount: 1); // 50% of what was answered

        var timedOut = quiz.TimeOutAttempt(attempt.Id, Now.AddMinutes(10));

        timedOut.Should().Be(attempt);
        attempt.Status.Should().Be(QuizAttemptStatus.TimedOut);
        attempt.ScorePercent.Should().Be(50);
        attempt.SubmittedAtUtc.Should().Be(attempt.DeadlineUtc); // time spent = the full window
        attempt.TimeSpentSeconds.Should().Be(600);
        quiz.DomainEvents.OfType<QuizAttemptTimedOutEvent>().Should().ContainSingle();
        quiz.DomainEvents.OfType<QuizGradedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void TimeOut_after_a_submit_is_a_no_op_so_the_timer_never_overturns_a_submission()
    {
        var quiz = Generate(Setting(count: 2, attempts: 2));
        var attempt = quiz.StartAttempt(Draws(Q(), Q()), Now);
        AnswerAttempt(quiz, attempt, correctCount: 2);
        quiz.SubmitAttempt(attempt.Id, Now);

        quiz.TimeOutAttempt(attempt.Id, Now.AddMinutes(31)).Should().BeNull();
        attempt.Status.Should().Be(QuizAttemptStatus.Submitted);
        attempt.ScorePercent.Should().Be(100);
    }

    [Fact]
    public void Attempt_snapshot_is_independent_of_later_bank_edits()
    {
        var quiz = Generate(Setting(count: 1, attempts: 2));
        var question = Q(mark: 2, body: "original");
        var attempt = quiz.StartAttempt(Draws(question), Now);
        var snapshot = attempt.Questions.Single();

        // Mutate the bank wholesale after the draw.
        question.Update(
            bodyLatex: "rewritten", mark: 9, isValidForQuiz: false, hintUrl: "h",
            optionDrafts: [new QuestionOptionDraft("C", false), new QuestionOptionDraft("D", true)]);

        snapshot.BodyLatex.Should().Be("original");
        snapshot.Mark.Should().Be(2);
        snapshot.Options.Select(o => o.Text).OrderBy(t => t).Should().Equal("A", "B");
        snapshot.Options.Single(o => o.IsCorrect).Text.Should().Be("A");
    }
}
