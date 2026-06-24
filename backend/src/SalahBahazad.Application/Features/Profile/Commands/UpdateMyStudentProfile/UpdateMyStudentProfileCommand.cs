using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Profile.DTOs;

namespace SalahBahazad.Application.Features.Profile.Commands.UpdateMyStudentProfile;

/// <summary>
/// Updates the signed-in student's own profile (Student-Portal S6 §A #2, FR-STU-PRO-001/002) — the seven writable
/// fields only. Grade is staff-managed and never changes here (§C.1); email is the Firebase identity and is not
/// stored at all (§C.2). Runs in the transaction pipeline (<see cref="ITransactionalRequest"/>) so the single
/// <c>SaveChanges</c> is audited by the interceptor (ActorType=Student, §E). Returns the re-read profile.
/// </summary>
public sealed record UpdateMyStudentProfileCommand(
    string FullName,
    string PhoneNumber,
    string SchoolName,
    Guid CityId,
    Guid RegionId,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary) : IRequest<StudentProfileDto>, ITransactionalRequest;
