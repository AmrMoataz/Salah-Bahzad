using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Staff.Commands.SetStaffActive;

/// <summary>Activates or deactivates a staff account (FR-ADM-STAFF-003). A member cannot deactivate themselves.</summary>
public sealed record SetStaffActiveCommand(Guid Id, bool IsActive) : IRequest<StaffDto>, ITransactionalRequest;
