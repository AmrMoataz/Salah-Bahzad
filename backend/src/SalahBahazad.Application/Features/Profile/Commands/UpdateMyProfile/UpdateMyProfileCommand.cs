using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Staff.DTOs;

namespace SalahBahazad.Application.Features.Profile.Commands.UpdateMyProfile;

/// <summary>
/// Updates the signed-in staff member's own display name (Settings → Profile, FR-ADM-SET-001).
/// Email is the Firebase identity and is not editable; password reset is delegated to Firebase.
/// </summary>
public sealed record UpdateMyProfileCommand(string DisplayName) : IRequest<StaffDto>, ITransactionalRequest;
