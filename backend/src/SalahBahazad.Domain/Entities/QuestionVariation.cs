using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// An alternate wording/image of a <see cref="Question"/> with its own correct answer, used to randomise
/// quizzes (FR-PLAT-QB-003). A child of the Question aggregate with its own owned option set. Variations
/// are uncapped. Managed only through the <see cref="Question"/> root.
/// </summary>
public sealed class QuestionVariation : EntityBase
{
    private readonly List<QuestionOption> _options = [];

    private QuestionVariation() { }

    public Guid QuestionId { get; private set; }
    public string? BodyLatex { get; private set; }

    /// <summary>R2 object key for the variation image; null when the body is LaTeX-only (FR-PLAT-QB-002).</summary>
    public string? ImageObjectKey { get; private set; }

    public IReadOnlyCollection<QuestionOption> Options => _options.AsReadOnly();

    internal static QuestionVariation Create(
        Guid questionId,
        string? bodyLatex,
        IReadOnlyList<QuestionOptionDraft> optionDrafts,
        string? imageObjectKey = null)
    {
        // A variation needs LaTeX text and/or an image; an image-only variation is allowed (FR-PLAT-QB-002).
        // The image (if any) is uploaded to storage *before* this call so its key is set atomically here.
        QuestionRules.RequireBody(bodyLatex, imageObjectKey);

        var variation = new QuestionVariation
        {
            QuestionId = questionId,
            BodyLatex = QuestionRules.Normalize(bodyLatex),
            ImageObjectKey = QuestionRules.Normalize(imageObjectKey),
        };
        variation._options.AddRange(QuestionRules.BuildOptions(optionDrafts));
        return variation;
    }

    internal void Update(string? bodyLatex, IReadOnlyList<QuestionOptionDraft> optionDrafts)
    {
        QuestionRules.RequireBody(bodyLatex, ImageObjectKey);
        BodyLatex = QuestionRules.Normalize(bodyLatex);
        _options.Clear();
        _options.AddRange(QuestionRules.BuildOptions(optionDrafts));
    }

    internal void SetImage(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ImageObjectKey = objectKey;
    }
}
