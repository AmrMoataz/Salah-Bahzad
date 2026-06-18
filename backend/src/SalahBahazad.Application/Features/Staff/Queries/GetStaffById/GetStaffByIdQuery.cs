using Mediator;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Staff.Queries.GetStaffById;

/// <summary>Returns a single staff member by id, or 404 if not found (within the caller's tenant).</summary>
public sealed record GetStaffByIdQuery(Guid Id) : IRequest<StaffDto>;
