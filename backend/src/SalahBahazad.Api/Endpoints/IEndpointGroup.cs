namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// A self-contained group of related endpoints that maps its own routes.
/// Implementations are discovered and invoked automatically by
/// <see cref="EndpointExtensions.MapEndpoints"/>, so adding a new endpoint group
/// never requires editing Program.cs (dotnet-claude-kit minimal-api auto-discovery pattern).
/// </summary>
public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}
