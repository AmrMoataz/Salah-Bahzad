using MockQueryable.NSubstitute;
using NSubstitute;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.UnitTests.Features.Taxonomy;

/// <summary>
/// Shared setup for taxonomy handler unit tests — a MockQueryable-backed <see cref="IAppDbContext"/>
/// (async LINQ, no real DB) plus an NSubstitute current-user resolver. The real EF global query filter
/// is not exercised here, so each test controls exactly what its sets contain.
/// </summary>
internal static class TaxonomyTestHelpers
{
    public static IAppDbContext DbWith(
        IEnumerable<Grade>? grades = null,
        IEnumerable<Subject>? subjects = null,
        IEnumerable<Specialization>? specializations = null)
    {
        // Build each mock set into a local FIRST: BuildMockDbSet() makes its own NSubstitute calls,
        // so inlining it inside .Returns(...) would corrupt NSubstitute's last-call tracking.
        var gradeSet = (grades ?? []).ToList().BuildMockDbSet();
        var subjectSet = (subjects ?? []).ToList().BuildMockDbSet();
        var specializationSet = (specializations ?? []).ToList().BuildMockDbSet();

        var db = Substitute.For<IAppDbContext>();
        db.Grades.Returns(gradeSet);
        db.Subjects.Returns(subjectSet);
        db.Specializations.Returns(specializationSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return db;
    }

    public static ICurrentUserResolver Actor(Guid? userId = null, Guid? tenantId = null)
    {
        var user = Substitute.For<ICurrentUserResolver>();
        user.UserId.Returns(userId ?? Guid.NewGuid());
        user.TenantId.Returns(tenantId ?? Guid.NewGuid());
        user.IsAuthenticated.Returns(true);
        return user;
    }
}
