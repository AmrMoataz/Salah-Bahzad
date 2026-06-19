using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.GetStudentById;

/// <summary>Loads a student's full 360° record (FR-ADM-STU-002).</summary>
public sealed record GetStudentByIdQuery(Guid Id) : IRequest<StudentDetailDto>;
