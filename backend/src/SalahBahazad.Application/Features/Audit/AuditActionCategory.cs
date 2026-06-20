namespace SalahBahazad.Application.Features.Audit;

/// <summary>
/// The backend-owned <c>action → category</c> map (contract §1/§5). <see cref="AuditFeedItem"/>'s
/// <c>category</c> drives the design's filter chips and the frontend's icon/accent choice, so it is derived
/// here from the raw <see cref="Domain.Entities.AuditEntry.Action"/> key — never invented per row.
/// <para>
/// Categories are lowercase strings (not a C# enum) so they serialise to the exact frozen-contract values
/// through the API's <c>JsonStringEnumConverter</c>-free string path (e.g. <c>"code"</c>, not <c>"Code"</c>).
/// The action keys are the real ones emitted today: explicit <c>AuditWriteRequest</c>s and the semantic
/// <c>IAuditableDomainEvent.AuditAction</c>s; generic interceptor field-diff rows (Created/Updated/Deleted)
/// and anything unmapped fall back to <see cref="Other"/>.
/// </para>
/// </summary>
public static class AuditActionCategory
{
    public const string Approval = "approval";
    public const string Code = "code";
    public const string Enrollment = "enrollment";
    public const string Session = "session";
    public const string Question = "question";
    public const string Device = "device";
    public const string Staff = "staff";
    public const string Student = "student";
    public const string Audit = "audit";
    public const string Other = "other";

    // action → category. Ordinal keys: action strings are stable internal identifiers, never localised.
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        // approval — the design's "Approvals" chip covers approve (check/green) and reject (x/red)
        ["StudentApproved"] = Approval,
        ["StudentRejected"] = Approval,

        // code — FR-PLAT-COD-*
        ["CodeBatchGenerated"] = Code,
        ["CodeRedeemed"] = Code,
        ["CodeDisabled"] = Code,
        ["CodeEnabled"] = Code,
        ["CodeDeleted"] = Code,
        ["CodesExported"] = Code,

        // enrollment — FR-PLAT-ENR-*
        ["EnrollmentCreated"] = Enrollment,
        ["EnrollmentExtended"] = Enrollment,
        ["EnrollmentRefunded"] = Enrollment,

        // session — FR-PLAT-SES-* (root + content: videos/materials)
        ["SessionCreated"] = Session,
        ["SessionUpdated"] = Session,
        ["SessionPublished"] = Session,
        ["SessionArchived"] = Session,
        ["SessionDeleted"] = Session,
        ["SessionThumbnailUpdated"] = Session,
        ["SessionPrerequisiteChanged"] = Session,
        ["SessionVideoAdded"] = Session,
        ["SessionVideoUpdated"] = Session,
        ["SessionVideoRemoved"] = Session,
        ["SessionMaterialAdded"] = Session,
        ["SessionMaterialRemoved"] = Session,

        // question — FR-PLAT-QB-* (question-bank edits, keyed to their session)
        ["QuestionAdded"] = Question,
        ["QuestionUpdated"] = Question,
        ["QuestionDetached"] = Question,
        ["QuestionImageUpdated"] = Question,
        ["QuestionImageRemoved"] = Question,
        ["QuestionVariationAdded"] = Question,
        ["QuestionVariationUpdated"] = Question,
        ["QuestionVariationRemoved"] = Question,
        ["QuestionVariationImageUpdated"] = Question,

        // device — FR-ADM-STU-010
        ["StudentDeviceCleared"] = Device,

        // student lifecycle — FR-STU-REG-*, FR-ADM-STU-006; the sensitive ID-image read is a student action
        ["StudentRegistered"] = Student,
        ["StudentDeactivated"] = Student,
        ["StudentReactivated"] = Student,
        ["StudentIdImageViewed"] = Student,
    };

    // Reverse index category → actions (case-insensitive lookups for the filter chip values).
    private static readonly Dictionary<string, string[]> ByCategory = Map
        .GroupBy(kv => kv.Value, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToArray(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Every action that has an explicit category — used to compute the <see cref="Other"/> complement.</summary>
    public static readonly string[] AllMappedActions = [.. Map.Keys];

    /// <summary>The category an action belongs to. Unmapped/generic rows (Created/Updated/Deleted) → <see cref="Other"/>;
    /// any future <c>Staff*</c> action is caught by prefix so the catalogue need not be edited in 5A.</summary>
    public static string CategoryOf(string? action)
    {
        if (string.IsNullOrEmpty(action))
            return Other;
        if (Map.TryGetValue(action, out var category))
            return category;
        if (action.StartsWith("Staff", StringComparison.Ordinal))
            return Staff;
        return Other;
    }

    /// <summary>The action keys in a category (for the SQL action-set filter). Unknown/empty categories — and the
    /// open-ended <see cref="Other"/>, which is a complement rather than a fixed set — return an empty list; the
    /// filter handles <see cref="Other"/> separately via <see cref="AllMappedActions"/>.</summary>
    public static IReadOnlyList<string> ActionsInCategory(string? category)
        => !string.IsNullOrWhiteSpace(category) && ByCategory.TryGetValue(category, out var actions)
            ? actions
            : [];
}
