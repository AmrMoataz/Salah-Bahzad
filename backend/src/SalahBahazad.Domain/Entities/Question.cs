using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// An MCQ question in a session's question bank (FR-PLAT-QB-001..006). A tenant-scoped root, soft-deleted
/// so generated assignments/quizzes and history survive a "detach" (FR-PLAT-ROLE-004, FR-ADM-QB-006). Owns
/// its answer options and a collection of <see cref="QuestionVariation"/> children. The body is LaTeX
/// and/or an uploaded image (FR-PLAT-QB-002); <see cref="IsValidForQuiz"/> gates quiz eligibility
/// (FR-PLAT-QB-004) and <see cref="HintUrl"/> is shown only in assignments (FR-PLAT-QB-005).
/// </summary>
public sealed class Question : TenantEntityBase, ISoftDeletable
{
    private readonly List<QuestionOption> _options = [];
    private readonly List<QuestionVariation> _variations = [];

    private Question() { }

    public Guid SessionId { get; private set; }
    public string? BodyLatex { get; private set; }

    /// <summary>R2 object key for the question image; null when the body is LaTeX-only (FR-PLAT-QB-002).</summary>
    public string? ImageObjectKey { get; private set; }

    public int Mark { get; private set; }
    public bool IsValidForQuiz { get; private set; }
    public string? HintUrl { get; private set; }

    public IReadOnlyCollection<QuestionOption> Options => _options.AsReadOnly();
    public IReadOnlyCollection<QuestionVariation> Variations => _variations.AsReadOnly();

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Question Create(
        Guid tenantId,
        Guid sessionId,
        string? bodyLatex,
        int mark,
        bool isValidForQuiz,
        string? hintUrl,
        IReadOnlyList<QuestionOptionDraft> optionDrafts)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("A question must belong to a session.", nameof(sessionId));
        ValidateMark(mark);
        // Image is uploaded separately (no file on the create call), so the body must be LaTeX here.
        QuestionRules.RequireBody(bodyLatex, imageObjectKey: null);

        var question = new Question
        {
            SessionId = sessionId,
            BodyLatex = QuestionRules.Normalize(bodyLatex),
            Mark = mark,
            IsValidForQuiz = isValidForQuiz,
            HintUrl = QuestionRules.Normalize(hintUrl),
        };
        question._options.AddRange(QuestionRules.BuildOptions(optionDrafts));
        question.SetTenant(tenantId);
        return question;
    }

    public void Update(
        string? bodyLatex,
        int mark,
        bool isValidForQuiz,
        string? hintUrl,
        IReadOnlyList<QuestionOptionDraft> optionDrafts)
    {
        ValidateMark(mark);
        QuestionRules.RequireBody(bodyLatex, ImageObjectKey);

        BodyLatex = QuestionRules.Normalize(bodyLatex);
        Mark = mark;
        IsValidForQuiz = isValidForQuiz;
        HintUrl = QuestionRules.Normalize(hintUrl);
        _options.Clear();
        _options.AddRange(QuestionRules.BuildOptions(optionDrafts));
    }

    public void SetImage(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ImageObjectKey = objectKey;
    }

    public void ClearImage()
    {
        if (string.IsNullOrWhiteSpace(BodyLatex))
            throw new InvalidOperationException(
                "Cannot remove the image: the question would have no content (FR-PLAT-QB-002).");
        ImageObjectKey = null;
    }

    public QuestionVariation AddVariation(string? bodyLatex, IReadOnlyList<QuestionOptionDraft> optionDrafts)
    {
        var variation = QuestionVariation.Create(Id, bodyLatex, optionDrafts);
        _variations.Add(variation);
        return variation;
    }

    public QuestionVariation UpdateVariation(
        Guid variationId, string? bodyLatex, IReadOnlyList<QuestionOptionDraft> optionDrafts)
    {
        var variation = FindVariation(variationId);
        variation.Update(bodyLatex, optionDrafts);
        return variation;
    }

    public QuestionVariation SetVariationImage(Guid variationId, string objectKey)
    {
        var variation = FindVariation(variationId);
        variation.SetImage(objectKey);
        return variation;
    }

    public QuestionVariation RemoveVariation(Guid variationId)
    {
        var variation = FindVariation(variationId);
        _variations.Remove(variation);
        return variation;
    }

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
    }

    private QuestionVariation FindVariation(Guid variationId)
        => _variations.FirstOrDefault(v => v.Id == variationId)
           ?? throw new InvalidOperationException($"Variation '{variationId}' is not part of this question.");

    private static void ValidateMark(int mark)
    {
        if (mark <= 0) throw new ArgumentOutOfRangeException(nameof(mark), "Mark must be greater than zero.");
    }
}
