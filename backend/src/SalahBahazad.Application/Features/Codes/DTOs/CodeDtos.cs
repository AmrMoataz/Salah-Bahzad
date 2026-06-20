using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;
using CodeEntity = SalahBahazad.Domain.Entities.Code;
using CodeBatchEntity = SalahBahazad.Domain.Entities.CodeBatch;

namespace SalahBahazad.Application.Features.Codes.DTOs;

/// <summary>A code register row (contract §1, scrCodes table: Serial, Value, Status, Batch, Session,
/// Redeemed by, Created). The UI labels <see cref="CodeStatus.Inactive"/> as "Disabled".</summary>
public sealed record CodeListDto(
    Guid Id,
    string Serial,
    decimal Value,
    CodeStatus Status,
    Guid BatchId,
    string? BatchLabel,
    Guid SessionId,
    string? SessionTitle,
    Guid? RedeemedByStudentId,
    string? RedeemedByStudentName,
    DateTimeOffset? RedeemedAtUtc,
    string? CreatedByName,
    DateTimeOffset CreatedAtUtc);

/// <summary>The "Batch ready" panel after a mint (contract §1, scrCodesGenerate) — no inline code list.</summary>
public sealed record CodeBatchDto(
    Guid BatchId,
    string Label,
    Guid SessionId,
    string? SessionTitle,
    decimal Value,
    int Quantity,
    DateTimeOffset CreatedAtUtc);

/// <summary>A generated CSV export ready to stream as a file download (#3/#4).</summary>
public sealed record CodeCsvFile(byte[] Content, string FileName);

/// <summary>Manual entity → DTO mappings (no AutoMapper, per backend/CLAUDE.md).</summary>
public static class CodeMappings
{
    public static CodeListDto ToListDto(
        this CodeEntity c,
        string? batchLabel,
        string? sessionTitle,
        string? redeemedByStudentName,
        string? createdByName) => new(
        c.Id,
        c.Serial,
        c.Value,
        c.Status,
        c.BatchId,
        batchLabel,
        c.SessionId,
        sessionTitle,
        c.RedeemedByStudentId,
        redeemedByStudentName,
        c.RedeemedAtUtc,
        createdByName,
        c.CreatedAtUtc);

    public static CodeBatchDto ToBatchDto(this CodeBatchEntity b, string? sessionTitle) => new(
        b.Id, b.Label, b.SessionId, sessionTitle, b.Value, b.Quantity, b.CreatedAtUtc);

    /// <summary>Projects a register row to one CSV line (contract §2 column set).</summary>
    public static CodeExportRow ToExportRow(this CodeListDto dto) => new(
        dto.Serial,
        dto.Value,
        dto.Status.ToString(),
        dto.BatchLabel,
        dto.SessionTitle,
        dto.CreatedByName,
        dto.CreatedAtUtc,
        dto.RedeemedByStudentName,
        dto.RedeemedAtUtc);
}
