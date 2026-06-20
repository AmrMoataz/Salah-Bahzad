using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// The provenance root for a mint of redemption <see cref="Code"/>s (FR-PLAT-COD-001). Immutable after
/// minting: it records the <see cref="Label"/>, target <see cref="SessionId"/>, per-code <see cref="Value"/>
/// and <see cref="Quantity"/>, and owns the codes it produced (created in the same transaction). Codes are
/// also a queryable aggregate of their own (the register pages them independently, FR-PLAT-COD-005).
/// </summary>
public sealed class CodeBatch : TenantEntityBase
{
    /// <summary>Inclusive bounds on a single mint (contract §1, FR-PLAT-COD-001).</summary>
    public const int MinQuantity = 1;
    public const int MaxQuantity = 1000;

    private readonly List<Code> _codes = [];

    private CodeBatch() { }

    /// <summary>Server-generated readable label, e.g. <c>CODES-20260620-01</c> (contract §5).</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>The session these codes redeem for (FR-PLAT-COD-003).</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Per-code face value (EGP); defaulted to the session price at mint (contract §5).</summary>
    public decimal Value { get; private set; }

    /// <summary>How many codes were minted.</summary>
    public int Quantity { get; private set; }

    /// <summary>The codes produced by this mint (managed through the root).</summary>
    public IReadOnlyCollection<Code> Codes => _codes.AsReadOnly();

    /// <summary>
    /// Mints a batch and its <paramref name="quantity"/> codes in one unit (FR-PLAT-COD-001). Each code gets a
    /// tenant-unique serial; seed <paramref name="existingSerials"/> with the tenant's current serials so the
    /// new ones never collide (the <c>(TenantId, Serial)</c> index is the final guarantee). Raises
    /// <see cref="CodeBatchGeneratedEvent"/> — the one audit entry for the whole mint.
    /// </summary>
    public static CodeBatch Generate(
        Guid tenantId,
        Guid sessionId,
        decimal value,
        int quantity,
        string label,
        ISet<string> existingSerials)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("A code batch must target a session.", nameof(sessionId));
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Code value cannot be negative.");
        if (quantity is < MinQuantity or > MaxQuantity)
            throw new ArgumentOutOfRangeException(
                nameof(quantity), $"Quantity must be between {MinQuantity} and {MaxQuantity}.");
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(existingSerials);

        var batch = new CodeBatch
        {
            Label = label.Trim(),
            SessionId = sessionId,
            Value = value,
            Quantity = quantity,
        };
        batch.SetTenant(tenantId);

        for (var i = 0; i < quantity; i++)
        {
            var serial = CodeSerialGenerator.NextUnique(existingSerials);
            batch._codes.Add(Code.Mint(tenantId, batch.Id, sessionId, value, serial));
        }

        batch.AddDomainEvent(new CodeBatchGeneratedEvent(batch.Id, batch.Label, quantity, value));
        return batch;
    }
}
