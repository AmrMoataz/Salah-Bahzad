using SalahBahazad.Application.Common.Interfaces;
using QuestionEntity = SalahBahazad.Domain.Entities.Question;
using QuestionOptionEntity = SalahBahazad.Domain.Entities.QuestionOption;
using QuestionVariationEntity = SalahBahazad.Domain.Entities.QuestionVariation;

namespace SalahBahazad.Application.Features.Questions.DTOs;

/// <summary>An MCQ answer option (FR-PLAT-QB-001).</summary>
public sealed record OptionDto(Guid Id, string Text, bool IsCorrect);

/// <summary>A question variation with its own body/image and options (FR-PLAT-QB-003).
/// <c>imageUrl</c> is a short-lived signed URL embedded for the editor preview.</summary>
public sealed record QuestionVariationDto(
    Guid Id,
    string? BodyLatex,
    string? ImageUrl,
    IReadOnlyList<OptionDto> Options);

/// <summary>A full bank question (FR-PLAT-QB-001..006). <c>imageUrl</c> is signed + embedded
/// (editor auto-preview). <c>hintUrl</c> is stored/returned but shown only in assignments.</summary>
public sealed record QuestionDto(
    Guid Id,
    Guid SessionId,
    string? BodyLatex,
    string? ImageUrl,
    int Mark,
    bool IsValidForQuiz,
    string? HintUrl,
    IReadOnlyList<OptionDto> Options,
    IReadOnlyList<QuestionVariationDto> Variations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Command-layer input for an option (the client may send an id on update; Phase 3 reassigns
/// option identity server-side, so it is not carried into the domain).</summary>
public sealed record QuestionOptionInput(string Text, bool IsCorrect);

/// <summary>Shared upload constraints for question/variation images (FR-PLAT-QB-002, FR-ADM-QB-003).</summary>
public static class QuestionImageConstraints
{
    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Maximum question-image size (5 MB).</summary>
    public const long MaxBytes = 5 * 1024 * 1024;
}

/// <summary>
/// Manual entity → DTO mappings. The question/variation image URLs are short-lived signed URLs, so the
/// question mappings are async (they call <see cref="IFileStorage"/>); option mapping stays synchronous.
/// </summary>
public static class QuestionMappings
{
    public static OptionDto ToDto(this QuestionOptionEntity o) => new(o.Id, o.Text, o.IsCorrect);

    public static async Task<QuestionVariationDto> ToDtoAsync(
        this QuestionVariationEntity v, IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var imageUrl = await SignAsync(v.ImageObjectKey, fileStorage, cancellationToken);
        return new QuestionVariationDto(
            v.Id,
            v.BodyLatex,
            imageUrl,
            v.Options.OrderBy(o => o.Order).Select(o => o.ToDto()).ToList());
    }

    public static async Task<QuestionDto> ToDtoAsync(
        this QuestionEntity q, IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var imageUrl = await SignAsync(q.ImageObjectKey, fileStorage, cancellationToken);

        var variations = new List<QuestionVariationDto>();
        foreach (var variation in q.Variations.OrderBy(v => v.CreatedAtUtc))
            variations.Add(await variation.ToDtoAsync(fileStorage, cancellationToken));

        return new QuestionDto(
            q.Id,
            q.SessionId,
            q.BodyLatex,
            imageUrl,
            q.Mark,
            q.IsValidForQuiz,
            q.HintUrl,
            q.Options.OrderBy(o => o.Order).Select(o => o.ToDto()).ToList(),
            variations,
            q.CreatedAtUtc,
            q.UpdatedAtUtc);
    }

    private static async Task<string?> SignAsync(
        string? objectKey, IFileStorage fileStorage, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(objectKey)
            ? null
            : (await fileStorage.GetSignedReadUrlAsync(objectKey, cancellationToken: cancellationToken)).Url;
}
