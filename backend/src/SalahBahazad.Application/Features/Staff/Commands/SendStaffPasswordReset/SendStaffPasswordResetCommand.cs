using Mediator;

namespace SalahBahazad.Application.Features.Staff.Commands.SendStaffPasswordReset;

/// <summary>
/// Asks Firebase to send a password-reset email to a staff member (FR-ADM-STAFF-002 / FR-PLAT-AUTH-009).
/// Password management is delegated entirely to Firebase self-service — the platform stores no passwords.
/// </summary>
public sealed record SendStaffPasswordResetCommand(Guid Id) : IRequest<StaffPasswordResetResponse>;

/// <summary>Confirms the address Firebase was asked to email the reset link to.</summary>
public sealed record StaffPasswordResetResponse(string Email);
