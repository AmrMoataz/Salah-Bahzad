using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Audit.DTOs;

/// <summary>
/// One activity-feed row (contract §1, design-anchored to <c>scrActivity</c> + the dashboard's "Recent activity"):
/// «icon» <b>actor</b> action <b>target</b> · when. The backend supplies the data and the backend-derived
/// <see cref="Category"/>; the frontend presentation layer owns the icon, accent colour and the action verb-phrase
/// (mirrors <c>code.presentation.ts</c>). Drill-in NAVIGATES to the affected entity via
/// <see cref="TargetType"/> + <see cref="TargetId"/> — there is no before/after-JSON detail endpoint in 5A.
/// </summary>
public sealed record AuditFeedItem(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string ActorType,            // "Staff" | "Student" | "System"
    string? ActorRole,           // "Teacher" | "Assistant" | null (narrowed to staff roles)
    string? ActorName,           // the bold ACTOR ("Mariam Adel"; "System" for system actions)
    string Action,               // RAW key — e.g. StudentApproved, CodeBatchGenerated, EnrollmentRefunded
    string Category,             // backend-derived from Action; drives the filter + the icon/accent (lowercase)
    string? Summary,             // full readable sentence (fallback text + tooltip)
    string? TargetType,          // = AuditEntry.EntityType (Student|Session|Code|Staff|…) — for the View link
    Guid? TargetId,              // = AuditEntry.EntityId — for the View link
    string? TargetLabel,         // resolved display name of the affected entity (the bold TARGET)
    string? Portal,              // "admin" | "student" | "system" | null
    string? IpAddress);

/// <summary>Manual entity → DTO mapping (no AutoMapper, per backend/CLAUDE.md). The resolved
/// <paramref name="actorName"/>/<paramref name="targetLabel"/>/<paramref name="category"/> are supplied by
/// <see cref="AuditFeedProjector"/>, which batches the lookups for a whole page.</summary>
public static class AuditMappings
{
    public static AuditFeedItem ToFeedItem(
        this AuditEntry a,
        string? actorName,
        string? actorRole,
        string? targetLabel,
        string category) => new(
        a.Id,
        a.OccurredAtUtc,
        a.ActorType,
        actorRole,
        actorName,
        a.Action,
        category,
        a.Summary,
        string.IsNullOrEmpty(a.EntityType) ? null : a.EntityType,
        a.EntityId,
        targetLabel,
        a.Portal,
        a.IpAddress);
}
