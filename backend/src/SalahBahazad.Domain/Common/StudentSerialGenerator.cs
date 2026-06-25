using System.Security.Cryptography;

namespace SalahBahazad.Domain.Common;

/// <summary>
/// Generates opaque, human-readable student serials in the form <c>STU-XXXXXX</c> using a Crockford base32
/// alphabet (no ambiguous I/L/O/U) — the watermark identity (FR-APP-VID-003). Serials are unique <b>per
/// tenant</b>; the <c>(TenantId, Serial)</c> unique index is the hard guarantee, while <see cref="NextUnique"/>
/// avoids collisions up-front against the serials already taken in the tenant. A deliberate sibling of
/// <see cref="CodeSerialGenerator"/> (kept separate so the <c>STU-</c> prefix and length stay independent of codes).
/// </summary>
public static class StudentSerialGenerator
{
    /// <summary>Crockford base32 — digits + letters excluding I, L, O and U to stay unambiguous on the watermark.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int GroupLength = 6;

    /// <summary>A single random serial (collision space ≈ 32^6 ≈ 1.07e9 per tenant).</summary>
    public static string Next() => $"STU-{RandomGroup()}";

    /// <summary>
    /// Returns a serial not present in <paramref name="taken"/>, adding it to the set so successive calls stay
    /// unique within a mint. Seed <paramref name="taken"/> with the tenant's existing serials.
    /// </summary>
    public static string NextUnique(ISet<string> taken, int maxAttempts = 1000)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var serial = Next();
            if (taken.Add(serial))
                return serial;
        }

        throw new InvalidOperationException("Unable to generate a unique student serial after many attempts.");
    }

    private static string RandomGroup() => new(RandomNumberGenerator.GetItems<char>(Alphabet, GroupLength));
}
