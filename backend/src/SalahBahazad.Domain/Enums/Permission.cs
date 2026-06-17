namespace SalahBahazad.Domain.Enums;

/// <summary>
/// Granular permission catalog (FR-PLAT-AUTH-007).
/// Roles are bundles of permissions, enabling reconfiguration without code changes.
/// </summary>
public enum Permission
{
    None = 0,

    // ── Students ──────────────────────────────────────
    StudentsRead        = 100,
    StudentsApprove     = 101,
    StudentsReject      = 102,
    StudentsEdit        = 103,
    StudentsDeactivate  = 104,
    StudentsDeviceClear = 105,

    // ── Sessions ──────────────────────────────────────
    SessionsRead    = 200,
    SessionsCreate  = 201,
    SessionsEdit    = 202,
    SessionsDelete  = 203,
    SessionsPublish = 204,

    // ── Codes (Teacher-only) ──────────────────────────
    CodesRead     = 300,
    CodesGenerate = 301,
    CodesDisable  = 302,
    CodesDelete   = 303,

    // ── Enrollments ───────────────────────────────────
    EnrollmentsRead   = 400,
    EnrollmentsUnlock = 401,
    EnrollmentsRefund = 402,

    // ── Question bank ─────────────────────────────────
    QuestionsRead   = 500,
    QuestionsCreate = 501,
    QuestionsEdit   = 502,
    QuestionsDelete = 503,

    // ── Taxonomy (Teacher-only) ───────────────────────
    TaxonomyRead   = 600,
    TaxonomyCreate = 601,
    TaxonomyEdit   = 602,
    TaxonomyDelete = 603,

    // ── Staff (Teacher-only) ──────────────────────────
    StaffRead       = 700,
    StaffCreate     = 701,
    StaffEdit       = 702,
    StaffDeactivate = 703,
    StaffDelete     = 704,

    // ── Attendance & reports ──────────────────────────
    AttendanceRead   = 800,
    AttendanceExport = 801,

    // ── Audit log ─────────────────────────────────────
    AuditRead          = 900,
    AuditReadSensitive = 901,

    // ── Dashboard ─────────────────────────────────────
    DashboardRead = 1000,
}
