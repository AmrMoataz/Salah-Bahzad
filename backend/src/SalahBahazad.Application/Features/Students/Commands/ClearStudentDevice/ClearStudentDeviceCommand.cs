using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.ClearStudentDevice;

/// <summary>
/// Clears a student's active bound device so the next sign-in may re-bind; a reason is mandatory and
/// audited (FR-PLAT-DEV-004, FR-ADM-STU-007).
/// </summary>
public sealed record ClearStudentDeviceCommand(Guid StudentId, string Reason) : IRequest<StudentDetailDto>;
