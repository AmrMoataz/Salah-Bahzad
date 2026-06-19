using Mediator;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.UpdateStudentContact;

/// <summary>Staff correction of a student's grade and parent contact numbers (FR-ADM-STU-005).</summary>
public sealed record UpdateStudentContactCommand(
    Guid Id,
    Guid GradeId,
    string PhoneNumber,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary) : IRequest<StudentDetailDto>;
