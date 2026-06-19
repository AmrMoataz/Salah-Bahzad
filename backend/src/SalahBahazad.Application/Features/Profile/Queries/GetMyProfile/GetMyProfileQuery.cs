using Mediator;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Profile.Queries.GetMyProfile;

/// <summary>Returns the signed-in staff member's own profile (Settings → Profile, FR-ADM-SET-001).</summary>
public sealed record GetMyProfileQuery : IRequest<StaffDto>;
