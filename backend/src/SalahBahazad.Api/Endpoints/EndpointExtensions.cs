namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Discovers every <see cref="IEndpointGroup"/> in this assembly and maps it once at startup.
/// Program.cs calls <see cref="MapEndpoints"/> a single time; new endpoint groups are picked up
/// automatically with no Program.cs change (dotnet-claude-kit minimal-api auto-discovery pattern).
/// </summary>
public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var groups = typeof(Program).Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpointGroup)) && t is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpointGroup>();

        foreach (var group in groups)
            group.Map(app);

        return app;
    }
}
