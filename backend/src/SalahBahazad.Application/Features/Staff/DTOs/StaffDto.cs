using SalahBahazad.Domain.Enums;
using StaffEntity = SalahBahazad.Domain.Entities.Staff;

namespace SalahBahazad.Application.Features.Staff.DTOs;

/// <summary>Staff member as returned by the admin portal (list rows and single record).</summary>
public sealed record StaffDto(
    Guid Id,
    string DisplayName,
    string Email,
    StaffRole Role,
    bool IsActive,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class StaffMappings
{
    public static StaffDto ToDto(this StaffEntity staff) => new(
        staff.Id,
        staff.DisplayName,
        staff.Email,
        staff.Role,
        staff.IsActive,
        staff.LastSeenAtUtc,
        staff.CreatedAtUtc,
        staff.UpdatedAtUtc);
}
