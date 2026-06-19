using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Taxonomy.DTOs;

/// <summary>A teacher-managed grade level as returned by the admin portal (FR-ADM-TAX-001).</summary>
public sealed record GradeDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class GradeMappings
{
    public static GradeDto ToDto(this Grade grade) => new(
        grade.Id,
        grade.Name,
        grade.CreatedAtUtc,
        grade.UpdatedAtUtc);
}
