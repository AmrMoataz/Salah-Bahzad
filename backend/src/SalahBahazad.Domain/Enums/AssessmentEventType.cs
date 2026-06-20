namespace SalahBahazad.Domain.Enums;

/// <summary>
/// A behaviour telemetry event captured while a student works on an assessment (FR-PLAT-ASG-004/005). Stored
/// in the high-volume <see cref="Entities.AssessmentEvent"/> table — never the audit log. Reused by 5B-2's
/// quiz focus-loss tracking.
/// </summary>
public enum AssessmentEventType
{
    /// <summary>The student opened/entered the assessment.</summary>
    Entered = 0,

    /// <summary>The student left/blurred the assessment (tab switch, navigation away).</summary>
    Left = 1,

    /// <summary>The student answered a question.</summary>
    Answered = 2,

    /// <summary>The student navigated between questions.</summary>
    Navigated = 3,

    /// <summary>The student lost focus during a quiz attempt (tab switch / app background) — monitoring only,
    /// never auto-forfeits (FR-PLAT-QZ-006).</summary>
    FocusLost = 4,

    /// <summary>The student returned focus to a quiz attempt after a <see cref="FocusLost"/>.</summary>
    FocusReturned = 5,
}
