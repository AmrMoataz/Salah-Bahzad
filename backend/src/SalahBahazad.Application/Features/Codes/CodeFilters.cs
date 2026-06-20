using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Codes;

/// <summary>
/// The shared register filter (FR-PLAT-COD-005), applied identically by <c>ListCodes</c> (paged) and the CSV
/// exports (whole filtered set) so #1 and #3 always agree. Free-text search matches the serial or the
/// redeemed-by student's name (contract §4).
/// </summary>
internal static class CodeFilters
{
    public static IQueryable<Code> Apply(
        IQueryable<Code> query,
        IAppDbContext db,
        string? search,
        CodeStatus? status,
        Guid? batchId,
        Guid? sessionId)
    {
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (batchId.HasValue)
            query = query.Where(c => c.BatchId == batchId.Value);

        if (sessionId.HasValue)
            query = query.Where(c => c.SessionId == sessionId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Serial.ToLower().Contains(term) ||
                (c.RedeemedByStudentId != null &&
                 db.Students.Any(s => s.Id == c.RedeemedByStudentId && s.FullName.ToLower().Contains(term))));
        }

        return query;
    }
}
