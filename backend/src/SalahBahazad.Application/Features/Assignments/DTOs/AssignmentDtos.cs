using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using UserAssignmentEntity = SalahBahazad.Domain.Entities.UserAssignment;

namespace SalahBahazad.Application.Features.Assignments.DTOs;

/// <summary>One MCQ option as shown to the student — no correctness leaked (contract §A).</summary>
public sealed record StudentAssignmentOptionDto(Guid Id, int Order, string Text);

/// <summary>One snapshotted question as shown to the student. <c>imageUrl</c> is a short-lived signed URL;
/// <c>hintUrl</c> is shown only in assignments (FR-PLAT-QB-005). No <c>isCorrect</c> is exposed.</summary>
public sealed record StudentAssignmentQuestionDto(
    Guid Id,
    int Order,
    string? BodyLatex,
    string? ImageUrl,
    string? HintUrl,
    IReadOnlyList<StudentAssignmentOptionDto> Options,
    Guid? SelectedOptionId);

/// <summary>The student's open-book assignment for a session (contract §A #1).</summary>
public sealed record StudentAssignmentDto(
    Guid Id,
    Guid SessionId,
    AssignmentStatus Status,
    int TimeSpentSeconds,
    IReadOnlyList<StudentAssignmentQuestionDto> Questions);

/// <summary>Progress after recording an answer (contract §A #2).</summary>
public sealed record AssignmentProgressDto(int AnsweredCount, int QuestionCount, AssignmentStatus Status);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md). Image keys are signed on read;
/// the student shape never carries option correctness.</summary>
public static class StudentAssignmentMappings
{
    public static async Task<StudentAssignmentDto> ToStudentDtoAsync(
        this UserAssignmentEntity assignment, IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var questions = new List<StudentAssignmentQuestionDto>();
        foreach (var q in assignment.Questions.OrderBy(q => q.Order))
        {
            var imageUrl = await SignAsync(q.ImageObjectKey, fileStorage, cancellationToken);
            questions.Add(new StudentAssignmentQuestionDto(
                q.Id,
                q.Order,
                q.BodyLatex,
                imageUrl,
                q.HintUrl,
                [.. q.Options.OrderBy(o => o.Order).Select(o => new StudentAssignmentOptionDto(o.Id, o.Order, o.Text))],
                q.SelectedOptionId));
        }

        return new StudentAssignmentDto(
            assignment.Id, assignment.SessionId, assignment.Status, assignment.TimeSpentSeconds, questions);
    }

    public static AssignmentProgressDto ToProgressDto(this UserAssignmentEntity assignment)
        => new(assignment.AnsweredCount, assignment.QuestionCount, assignment.Status);

    private static async Task<string?> SignAsync(
        string? objectKey, IFileStorage fileStorage, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(objectKey)
            ? null
            : (await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken)).Url;
}
