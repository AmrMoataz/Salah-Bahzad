using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// The per-(student, session) attendance/progress record (FR-PLAT-ATT-001). Phase 4 creates the <b>shell</b>
/// on enrol so the enrollment transaction is complete; the score columns are written by the Phase 5 grading
/// engine. Tenant-scoped; <see cref="IAuditViaEventOnly"/> so the shell's creation is covered by the
/// enrollment's semantic event rather than a row of its own.
/// </summary>
public sealed class Attendance : TenantEntityBase, IAuditViaEventOnly
{
    private Attendance() { }

    public Guid StudentId { get; private set; }
    public Guid SessionId { get; private set; }

    /// <summary>The enrollment that created this shell (the single row per student+session).</summary>
    public Guid EnrollmentId { get; private set; }

    // ── Phase 5 grading writes these (nullable/zero until then) ────────────────
    public int? AssignmentScore { get; private set; }
    public int? BestQuizPercent { get; private set; }
    public int VideosWatched { get; private set; }

    public static Attendance CreateShell(Guid tenantId, Guid studentId, Guid sessionId, Guid enrollmentId)
    {
        var attendance = new Attendance
        {
            StudentId = studentId,
            SessionId = sessionId,
            EnrollmentId = enrollmentId,
            VideosWatched = 0,
        };
        attendance.SetTenant(tenantId);
        return attendance;
    }

    /// <summary>
    /// Records the auto-graded assignment score as a 0–100 percent (FR-PLAT-ASG-006, FR-PLAT-ATT-002). Written
    /// by the grading event handler attributed to the System actor; idempotent re-writes keep the latest score.
    /// </summary>
    public void SetAssignmentScore(int percent)
    {
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Assignment score must be between 0 and 100.");
        AssignmentScore = percent;
    }
}
