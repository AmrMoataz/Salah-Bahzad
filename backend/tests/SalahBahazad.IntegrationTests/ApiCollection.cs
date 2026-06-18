namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Shares a single <see cref="SalahBahazadApiFactory"/> (one Postgres container + host) across all
/// integration test classes, so the container is started once for the whole suite.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SalahBahazadApiFactory>
{
    public const string Name = "Salah Bahzad API";
}
