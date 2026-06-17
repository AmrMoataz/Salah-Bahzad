namespace SalahBahazad.Domain.Entities;

/// <summary>
/// Platform tenant. Single tenant today; the TenantId seam is present throughout
/// to enable multi-tenancy without a schema rewrite (FR-PLAT-TEN-001/004).
/// </summary>
public sealed class Tenant : Common.EntityBase
{
    private Tenant() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public string? Locale { get; private set; }
    public string? TimeZone { get; private set; }

    public static Tenant Create(string name, string slug, string? locale = null, string? timeZone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Tenant
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Locale = locale,
            TimeZone = timeZone,
        };
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
