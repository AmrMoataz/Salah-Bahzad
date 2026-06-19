using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Staff.Commands.CreateStaff;

/// <summary>
/// Creates a staff member (assistant/teacher) and provisions their Firebase account (FR-ADM-STAFF-001).
/// The actor cannot assign a role higher than their own (FR-PLAT-ROLE-002).
/// </summary>
public sealed record CreateStaffCommand(
    string DisplayName,
    string Email,
    StaffRole Role) : IRequest<StaffDto>, ITransactionalRequest;
