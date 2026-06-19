using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Taxonomy.DTOs;

/// <summary>
/// A teacher-managed subject as returned by the admin portal (FR-ADM-TAX-001).
/// <see cref="SpecializationCount"/> drives the "in use" affordance — a subject with live
/// specializations cannot be deleted, only archived (FR-PLAT-TAX-004).
/// </summary>
public sealed record SubjectDto(
    Guid Id,
    string Name,
    int SpecializationCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class SubjectMappings
{
    /// <summary>
    /// Maps a subject plus its live-specialization count (computed by the query handler, since the
    /// count is relational state the entity does not carry).
    /// </summary>
    public static SubjectDto ToDto(this Subject subject, int specializationCount) => new(
        subject.Id,
        subject.Name,
        specializationCount,
        subject.CreatedAtUtc,
        subject.UpdatedAtUtc);
}
