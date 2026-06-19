using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Taxonomy.DTOs;

/// <summary>
/// A teacher-managed specialization as returned by the admin portal (FR-ADM-TAX-001). Carries its
/// owning <see cref="SubjectId"/> and <see cref="SubjectName"/> so the UI can show the
/// Subject → Specialization hierarchy without a second lookup (FR-PLAT-TAX-002).
/// </summary>
public sealed record SpecializationDto(
    Guid Id,
    string Name,
    Guid SubjectId,
    string SubjectName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class SpecializationMappings
{
    /// <summary>
    /// Maps a specialization plus its owning subject's name (resolved by the query handler, since the
    /// name lives on the related <see cref="Subject"/>).
    /// </summary>
    public static SpecializationDto ToDto(this Specialization specialization, string subjectName) => new(
        specialization.Id,
        specialization.Name,
        specialization.SubjectId,
        subjectName,
        specialization.CreatedAtUtc,
        specialization.UpdatedAtUtc);
}
