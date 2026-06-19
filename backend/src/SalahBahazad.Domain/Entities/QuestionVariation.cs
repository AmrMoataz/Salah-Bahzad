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
        Guid questionId, string? bodyLatex, IReadOnlyList<QuestionOptionDraft> optionDrafts)
    {
        // Image is uploaded in a separate step (no file on the create call), so the body must be LaTeX here.
        QuestionRules.RequireBody(bodyLatex, imageObjectKey: null);

        var variation = new QuestionVariation
        {
            QuestionId = questionId,
            BodyLatex = QuestionRules.Normalize(bodyLatex),
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
