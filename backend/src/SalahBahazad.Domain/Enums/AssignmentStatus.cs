namespace SalahBahazad.Domain.Enums;

/// <summary>
/// State of a student's <see cref="Entities.UserAssignment"/> (FR-PLAT-ASG-002/006). Open-access and
/// resumable: it stays <see cref="InProgress"/> across sittings until every question is answered, then is
/// auto-graded to <see cref="Completed"/> — an immutable result thereafter.
/// </summary>
public enum AssignmentStatus
{
    /// <summary>Still being solved; answers are saved and may be changed until completion.</summary>
    InProgress = 0,

    /// <summary>Every question answered and the assignment auto-graded — result is immutable.</summary>
    Completed = 1,
}
