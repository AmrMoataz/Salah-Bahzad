using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalahBahazad.Application.Features.Auth.Commands.ExchangeFirebaseToken;
using SalahBahazad.Application.Features.Auth.Commands.ExchangeStudentFirebaseToken;
using SalahBahazad.Application.Features.Auth.Commands.RefreshToken;
using SalahBahazad.Application.Features.Auth.DTOs;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Authentication endpoints (FR-ADM-AUTH-001, FR-STU-AUTH-001, FR-PLAT-AUTH-002).
/// Firebase token exchange → platform JWT pair, for staff and students on separate routes.
/// </summary>
internal sealed class AuthEndpoints : IEndpointGroup
{
    /// <summary>The HttpOnly device-binding cookie (FR-PLAT-DEV-005, frozen contract §1.3).</summary>
    private const string DeviceCookieName = "sb_device";
    private const string FingerprintHeader = "X-Device-Fingerprint";
    private static readonly TimeSpan DeviceCookieLifetime = TimeSpan.FromDays(365);

    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .WithOpenApi();

        group.MapPost("/exchange", ExchangeAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous()
            .WithName("ExchangeFirebaseToken")
            .WithSummary("Exchange a staff Firebase ID token for a platform JWT pair")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        group.MapPost("/student/exchange", ExchangeStudentAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous()
            .WithName("ExchangeStudentFirebaseToken")
            .WithSummary("Exchange a student Firebase ID token for a Student-role JWT pair (status-gated, device-bound)")
            .Produces<StudentAuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        // Role-aware: returns AuthTokenResponse for a staff refresh token, StudentAuthResponse for a student's.
        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("auth")
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Exchange a valid refresh token for a new platform JWT pair (staff or student)")
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

    private static async Task<IResult> ExchangeStudentAsync(
        [FromBody] StudentExchangeRequest request,
        ISender sender,
        HttpContext httpContext,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var rawDeviceToken = httpContext.Request.Cookies[DeviceCookieName];
        var fingerprint = httpContext.Request.Headers[FingerprintHeader].FirstOrDefault();

        var command = new ExchangeStudentFirebaseTokenCommand(
            request.FirebaseIdToken,
            rawDeviceToken,
            fingerprint,
            ip);

        var result = await sender.Send(command, cancellationToken);

        // On a successful bind/reuse, (re-)issue the long-lived HttpOnly device cookie (§1.3).
        if (result.DeviceTokenToSet is { } token)
            httpContext.Response.Cookies.Append(DeviceCookieName, token, BuildDeviceCookieOptions(environment));

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);

        // Each client receives its own wire shape; only one of the two is ever set.
        return result.Student is not null
            ? Results.Ok(result.Student)
            : Results.Ok(result.Staff);
    }

    /// <summary>
    /// Device-cookie options (§1.3): HttpOnly + Secure always; SameSite=Lax in dev (same-origin via the
    /// Angular proxy), SameSite=None in staging/prod (cross-origin portal↔API). Path=/, long-lived.
    /// </summary>
    private static CookieOptions BuildDeviceCookieOptions(IHostEnvironment environment) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Path = "/",
        MaxAge = DeviceCookieLifetime,
        IsEssential = true,
    };
}

/// <summary>Request body for the staff token exchange endpoint.</summary>
internal sealed record ExchangeFirebaseTokenRequest(string FirebaseIdToken);

/// <summary>Request body for the student token exchange endpoint (device token rides the sb_device cookie).</summary>
internal sealed record StudentExchangeRequest(string FirebaseIdToken);

/// <summary>Request body for the refresh endpoint.</summary>
internal sealed record RefreshTokenRequest(string RefreshToken);
