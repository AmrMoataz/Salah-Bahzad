using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalahBahazad.Application.Features.Auth.Commands.ExchangeFirebaseToken;
using SalahBahazad.Application.Features.Auth.Commands.RefreshToken;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Staff authentication endpoints (FR-ADM-AUTH-001, FR-PLAT-AUTH-002).
/// Firebase token exchange → platform JWT pair.
/// </summary>
internal sealed class AuthEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .WithOpenApi();

        group.MapPost("/exchange", ExchangeAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous()
            .WithName("ExchangeFirebaseToken")
            .WithSummary("Exchange a Firebase ID token for a platform JWT pair")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Exchange a valid refresh token for a new platform JWT pair")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> ExchangeAsync(
        [FromBody] ExchangeFirebaseTokenRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var deviceId = httpContext.Request.Headers["X-Device-Id"].FirstOrDefault();

        var command = new ExchangeFirebaseTokenCommand(
            request.FirebaseIdToken,
            ip,
            deviceId);

        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

/// <summary>Request body for the token exchange endpoint.</summary>
internal sealed record ExchangeFirebaseTokenRequest(string FirebaseIdToken);

/// <summary>Request body for the refresh endpoint.</summary>
internal sealed record RefreshTokenRequest(string RefreshToken);
