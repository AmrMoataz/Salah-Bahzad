using Mediator;

namespace SalahBahazad.Application.Features.Staff.Commands.DeleteStaff;

/// <summary>Soft-deletes a staff account, preserving audit attribution (FR-ADM-STAFF-003). Cannot delete self.</summary>
public sealed record DeleteStaffCommand(Guid Id) : IRequest<Unit>;
