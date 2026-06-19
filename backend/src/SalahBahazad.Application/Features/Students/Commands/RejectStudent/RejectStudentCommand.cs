using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.RejectStudent;

/// <summary>Rejects a pending student; a reason is mandatory, stored, and audited (FR-ADM-STU-004).</summary>
public sealed record RejectStudentCommand(Guid Id, string Reason) : IRequest<StudentDetailDto>;
