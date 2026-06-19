using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.SetStudentActive;

/// <summary>Deactivates or re-activates a student account (FR-ADM-STU-006). Audited automatically.</summary>
public sealed record SetStudentActiveCommand(Guid Id, bool IsActive) : IRequest<StudentDetailDto>;
