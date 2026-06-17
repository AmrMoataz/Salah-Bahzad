using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Resolves the authenticated staff member from the platform JWT claim set.
/// </summary>
public interface ICurrentUserResolver
{
    Guid UserId { get; }
    Guid TenantId { get; }
    StaffRole Role { get; }
    string? DeviceId { get; }
    bool IsAuthenticated { get; }
}
