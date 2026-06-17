using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Auth;

/// <summary>
/// Maps roles to their default permission bundles (FR-PLAT-AUTH-007, FR-PLAT-ROLE-003).
/// Teacher gets everything; Assistant gets operational but not administrative permissions.
/// </summary>
public static class PermissionCatalog
{
    private static readonly IReadOnlyList<Permission> TeacherPermissions =
    [
        Permission.StudentsRead, Permission.StudentsApprove, Permission.StudentsReject,
        Permission.StudentsEdit, Permission.StudentsDeactivate, Permission.StudentsDeviceClear,
        Permission.SessionsRead, Permission.SessionsCreate, Permission.SessionsEdit,
        Permission.SessionsDelete, Permission.SessionsPublish,
        Permission.CodesRead, Permission.CodesGenerate, Permission.CodesDisable, Permission.CodesDelete,
        Permission.EnrollmentsRead, Permission.EnrollmentsUnlock, Permission.EnrollmentsRefund,
        Permission.QuestionsRead, Permission.QuestionsCreate, Permission.QuestionsEdit, Permission.QuestionsDelete,
        Permission.TaxonomyRead, Permission.TaxonomyCreate, Permission.TaxonomyEdit, Permission.TaxonomyDelete,
        Permission.StaffRead, Permission.StaffCreate, Permission.StaffEdit,
        Permission.StaffDeactivate, Permission.StaffDelete,
        Permission.AttendanceRead, Permission.AttendanceExport,
        Permission.AuditRead, Permission.AuditReadSensitive,
        Permission.DashboardRead,
    ];

    private static readonly IReadOnlyList<Permission> AssistantPermissions =
    [
        Permission.StudentsRead, Permission.StudentsApprove, Permission.StudentsReject,
        Permission.StudentsEdit, Permission.StudentsDeviceClear,
        Permission.SessionsRead,
        Permission.CodesRead,
        Permission.EnrollmentsRead, Permission.EnrollmentsUnlock,
        Permission.QuestionsRead,
        Permission.TaxonomyRead,
        Permission.StaffRead,
        Permission.AttendanceRead, Permission.AttendanceExport,
        Permission.AuditRead,
        Permission.DashboardRead,
    ];

    public static IReadOnlyList<Permission> ForRole(StaffRole role) => role switch
    {
        StaffRole.Teacher => TeacherPermissions,
        StaffRole.Assistant => AssistantPermissions,
        _ => [],
    };
}
