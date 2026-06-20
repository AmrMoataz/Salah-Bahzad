using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Review;

/// <summary>
/// Builds the human-readable label for a behaviour timeline row (contract §C, e.g. "Answered Q1"). The icon and
/// accent are the frontend's; the backend owns the label text so the API reads sensibly on its own.
/// </summary>
internal static class BehaviourLabel
{
    public static string For(AssessmentEventType type, int? questionOrder) => type switch
    {
        AssessmentEventType.Entered => "Entered",
        AssessmentEventType.Left => "Left",
        AssessmentEventType.Answered => questionOrder is int a ? $"Answered Q{a}" : "Answered",
        AssessmentEventType.Navigated => questionOrder is int n ? $"Navigated to Q{n}" : "Navigated",
        _ => type.ToString(),
    };
}
