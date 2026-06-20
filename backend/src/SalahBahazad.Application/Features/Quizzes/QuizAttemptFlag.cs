using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Quizzes;

/// <summary>
/// Derives the attempt "flag" string from its status (contract §A/§B): <c>Timeout</c> for a timed-out attempt,
/// <c>Forfeit</c> for a forfeited one, else <c>Clean</c> (submitted or in-progress). The frontend maps the flag
/// to its pill style; the backend owns the text so the API reads sensibly on its own.
/// </summary>
internal static class QuizAttemptFlag
{
    public static string For(QuizAttemptStatus status) => status switch
    {
        QuizAttemptStatus.TimedOut => "Timeout",
        QuizAttemptStatus.Forfeited => "Forfeit",
        _ => "Clean",
    };
}
