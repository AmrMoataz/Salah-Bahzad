using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Mints and verifies the device-binding token (FR-PLAT-DEV-005). The raw token is
/// <c>base64url(payload) "." base64url(HMAC-SHA256(payload))</c> over <c>{studentId, deviceGuid, issuedAt}</c>,
/// signed with <c>Device:SigningKey</c> (falling back to <c>Jwt:Secret</c>) — no secret committed
/// (NFR-SEC-002). The database stores only <c>base64(SHA256(rawToken))</c>: the raw token is a credential
/// that lives solely in the HttpOnly <c>sb_device</c> cookie, so a database leak cannot replay it. A token
/// whose HMAC does not validate is treated as forged (<see cref="Verify"/> returns null → unrecognised
/// device).
/// </summary>
internal sealed class DeviceBindingService(IConfiguration configuration, TimeProvider clock) : IDeviceBindingService
{
    private const int MaxFingerprintLength = 512; // matches StudentDevice.FingerprintSummary column

    public (string RawToken, string Hash) Issue(Guid studentId, Guid deviceGuid)
    {
        var issuedAt = clock.GetUtcNow().ToUnixTimeSeconds();
        var payload = Encoding.UTF8.GetBytes($"{studentId:N}.{deviceGuid:N}.{issuedAt}");

        var signature = Sign(payload);
        var rawToken = $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(signature)}";
        return (rawToken, HashRawToken(rawToken));
    }

    public string? Verify(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
            return null;

        var separator = rawToken.IndexOf('.');
        if (separator <= 0 || separator == rawToken.Length - 1)
            return null;

        byte[] payload;
        byte[] presentedSignature;
        try
        {
            payload = Base64Url.DecodeFromChars(rawToken.AsSpan(0, separator));
            presentedSignature = Base64Url.DecodeFromChars(rawToken.AsSpan(separator + 1));
        }
        catch (FormatException)
        {
            return null;
        }

        var expectedSignature = Sign(payload);
        return CryptographicOperations.FixedTimeEquals(expectedSignature, presentedSignature)
            ? HashRawToken(rawToken)
            : null;
    }

    public string? Summarize(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return null;

        var trimmed = fingerprint.Trim();
        return trimmed.Length <= MaxFingerprintLength ? trimmed : trimmed[..MaxFingerprintLength];
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(GetSigningKey(), payload);

    private static string HashRawToken(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private byte[] GetSigningKey()
    {
        var secret = configuration["Device:SigningKey"]
            ?? configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Neither Device:SigningKey nor Jwt:Secret is configured.");
        return Encoding.UTF8.GetBytes(secret);
    }
}
