using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.ApproveStudent;

/// <summary>Approves a pending student, enabling sign-in (FR-ADM-STU-003). Audited automatically.</summary>
public sealed record ApproveStudentCommand(Guid Id) : IRequest<StudentDetailDto>;
