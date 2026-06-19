using System.Security.Cryptography;
using System.Text;

namespace SalahBahazad.Infrastructure.Persistence.SeedData;

/// <summary>
/// Derives a stable <see cref="Guid"/> from a string key (RFC 4122 v3-style, namespaced MD5).
/// Used so seeded reference rows (cities/regions) get identical ids across every environment and
/// across repeated <c>dotnet ef migrations add</c> runs — a requirement for EF <c>HasData</c> not to
/// produce spurious diffs and for student FKs to reference the same ids everywhere.
/// </summary>
internal static class DeterministicGuid
{
    // Fixed namespace — changing this would re-key every seeded id, so it must never change.
    private static readonly byte[] NamespaceBytes =
        new Guid("8f8b6b2e-3d4a-4c6e-9b1a-7e2d5f0a1c33").ToByteArray();

    public static Guid Create(string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var buffer = new byte[NamespaceBytes.Length + keyBytes.Length];
        Buffer.BlockCopy(NamespaceBytes, 0, buffer, 0, NamespaceBytes.Length);
        Buffer.BlockCopy(keyBytes, 0, buffer, NamespaceBytes.Length, keyBytes.Length);

        var hash = MD5.HashData(buffer); // 16 bytes — deterministic, non-cryptographic use only.

        // Stamp version (3) and RFC 4122 variant bits so the value is a well-formed UUID.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash);
    }
}
