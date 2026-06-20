using System.Security.Cryptography;

namespace SalahBahazad.Domain.Common;

/// <summary>
/// Generates opaque, human-keyable code serials in the form <c>SB-XXXXX-XXXXX</c> using a Crockford base32
/// alphabet (no ambiguous I/L/O/U) — contract §5, FR-PLAT-COD-001. Serials are unique <b>per tenant</b>; the
/// <c>(TenantId, Serial)</c> unique index is the hard guarantee, while <see cref="NextUnique"/> avoids
/// collisions up-front against the serials already taken in the tenant (and within the batch being minted).
/// </summary>
public static class CodeSerialGenerator
{
    /// <summary>Crockford base32 — digits + letters excluding I, L, O and U to stay unambiguous when typed.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int GroupLength = 5;

    /// <summary>A single random serial (collision space ≈ 32^10 ≈ 1.1e15 per tenant).</summary>
    public static string Next() => $"SB-{RandomGroup()}-{RandomGroup()}";

    /// <summary>
    /// Returns a serial not present in <paramref name="taken"/>, adding it to the set so successive calls
    /// stay unique within a mint. Seed <paramref name="taken"/> with the tenant's existing serials.
    /// </summary>
    public static string NextUnique(ISet<string> taken, int maxAttempts = 1000)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var serial = Next();
            if (taken.Add(serial))
                return serial;
        }

        throw new InvalidOperationException("Unable to generate a unique code serial after many attempts.");
    }

    private static string RandomGroup() => new(RandomNumberGenerator.GetItems<char>(Alphabet, GroupLength));
}
