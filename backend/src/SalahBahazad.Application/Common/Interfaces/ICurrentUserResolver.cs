using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Resolves the authenticated staff member from the platform JWT claim set.
/// </summary>
public interface ICurrentUserResolver
{
    Guid UserId { get; }
    Guid TenantId { get; }

    /// <summary>The staff role from the JWT, or <see cref="StaffRole.None"/> for a non-staff principal
    /// (e.g. a Student-role token redeeming a code) — never throws on a non-staff role claim.</summary>
    StaffRole Role { get; }

    string? DeviceId { get; }
    bool IsAuthenticated { get; }

    /// <summary>Audit actor classification: "Staff" | "Student" | "System" (unauthenticated).</summary>
    string ActorType { get; }
}
