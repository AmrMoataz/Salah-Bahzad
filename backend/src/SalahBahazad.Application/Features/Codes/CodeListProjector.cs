using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Codes.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Codes;

/// <summary>
/// Resolves the display joins for a set of <see cref="Code"/> rows (batch label, session title, redeemed-by
/// student name, created-by staff name) and projects them to <see cref="CodeListDto"/> — shared by the
/// register list and the CSV exports (FR-PLAT-COD-005). Names are looked up with <c>IgnoreQueryFilters</c> so
/// an archived session/student still shows its name; the ids all come from the caller's tenant-scoped codes.
/// </summary>
internal static class CodeListProjector
{
    public static async Task<List<CodeListDto>> ToListDtosAsync(
        IAppDbContext db, IReadOnlyList<Code> codes, CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
            return [];

        var batchIds = codes.Select(c => c.BatchId).Distinct().ToList();
        var batchLabels = await db.CodeBatches
            .IgnoreQueryFilters()
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Label, cancellationToken);

        var sessionIds = codes.Select(c => c.SessionId).Distinct().ToList();
        var sessionTitles = await db.Sessions
            .IgnoreQueryFilters()
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Title, cancellationToken);

        var studentIds = codes
            .Where(c => c.RedeemedByStudentId.HasValue)
            .Select(c => c.RedeemedByStudentId!.Value)
            .Distinct()
            .ToList();
        var studentNames = studentIds.Count == 0
            ? []
            : await db.Students
                .IgnoreQueryFilters()
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var creatorIds = codes
            .Where(c => c.CreatedById.HasValue)
            .Select(c => c.CreatedById!.Value)
            .Distinct()
            .ToList();
        var creatorNames = creatorIds.Count == 0
            ? []
            : await db.Staff
                .IgnoreQueryFilters()
                .Where(s => creatorIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.DisplayName, cancellationToken);

        return [.. codes.Select(c => c.ToListDto(
            batchLabels.GetValueOrDefault(c.BatchId),
            sessionTitles.GetValueOrDefault(c.SessionId),
            c.RedeemedByStudentId is Guid sid ? studentNames.GetValueOrDefault(sid) : null,
            c.CreatedById is Guid cid ? creatorNames.GetValueOrDefault(cid) : null))];
    }
}
