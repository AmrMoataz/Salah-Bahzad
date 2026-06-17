using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>Resolves the authenticated staff member from the platform JWT (FR-PLAT-AUTH-002).</summary>
internal sealed class CurrentUserResolver(IHttpContextAccessor httpContextAccessor) : ICurrentUserResolver
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public Guid TenantId =>
        Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue("tenant_id")!);

    public StaffRole Role =>
        Enum.Parse<StaffRole>(httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.Role)!);

    public string? DeviceId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue("device_id");
}
