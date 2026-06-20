using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Quizzes;

/// <summary>
/// Backend-owned quiz randomisation (contract §F, FR-PLAT-QZ-003): draws an independently randomised subset of
/// <c>count</c> distinct quiz-eligible questions and renders each as a random <b>form</b> (the base question or
/// one of its variations, FR-PLAT-QB-003), so every attempt differs and the chosen options are copied into the
/// immutable snapshot. A partial Fisher–Yates shuffle keeps the pick uniform and distinct. <see cref="Random"/>
/// is injected so the strategy is deterministically testable; production passes <see cref="Random.Shared"/>.
/// </summary>
internal static class QuizQuestionSelector
{
    public static IReadOnlyList<QuizQuestionForm> Draw(
        IReadOnlyList<Question> eligible, int count, Random random)
    {
        ArgumentNullException.ThrowIfNull(eligible);
        ArgumentNullException.ThrowIfNull(random);

        // Never draw more than the bank holds (settings cap QuestionCount ≤ eligible, but a later soft-delete
        // could shrink it — clamp defensively so generation/start never throws on a thin bank).
        var take = Math.Min(count, eligible.Count);

        var pool = eligible.ToList();
        for (var i = 0; i < take; i++)
        {
            var j = random.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return [.. pool.Take(take).Select(q => PickForm(q, random))];
    }

    /// <summary>Picks the base form or one variation uniformly, copying that form's options into the draw.</summary>
    private static QuizQuestionForm PickForm(Question question, Random random)
    {
        var variations = question.Variations;
        if (variations.Count > 0)
        {
            var pick = random.Next(variations.Count + 1); // 0 = base form, 1..n = variations
            if (pick > 0)
            {
                var variation = variations.ElementAt(pick - 1);
                return new QuizQuestionForm(
                    question.Id, variation.BodyLatex, variation.ImageObjectKey, question.Mark,
                    [.. variation.Options]);
            }
        }

        return new QuizQuestionForm(
            question.Id, question.BodyLatex, question.ImageObjectKey, question.Mark, [.. question.Options]);
    }
}
