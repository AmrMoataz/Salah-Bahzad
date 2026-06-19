using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.GetStudentIdImageUrl;

/// <summary>
/// Issues a short-lived signed URL for a student's ID-verification image and audits the access
/// (FR-PLAT-AST-003, NFR-PRIV-001/002).
/// </summary>
public sealed record GetStudentIdImageUrlQuery(Guid StudentId) : IRequest<StudentIdImageUrlDto>;
