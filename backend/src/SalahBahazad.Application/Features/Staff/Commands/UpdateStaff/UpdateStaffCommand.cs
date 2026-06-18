using Mediator;
using SalahBahazad.Application.Features.Staff.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Staff.Commands.UpdateStaff;

/// <summary>Updates a staff member's details and role (FR-ADM-STAFF-002), enforcing no-escalation.</summary>
public sealed record UpdateStaffCommand(
    Guid Id,
    string DisplayName,
    string Email,
    StaffRole Role) : IRequest<StaffDto>;
