using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Taxonomy.Specializations.Commands.CreateSpecialization;
using SalahBahazad.Domain.Entities;
using static SalahBahazad.UnitTests.Features.Taxonomy.TaxonomyTestHelpers;

namespace SalahBahazad.UnitTests.Features.Taxonomy;

/// <summary>
/// Specialization creation validates its owning subject exists and is unique within that subject
/// (FR-PLAT-TAX-002).
/// </summary>
public class CreateSpecializationHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Creates_under_existing_subject_and_returns_subject_name()
    {
        var subject = Subject.Create(Tenant, "Physics");
        var db = DbWith(subjects: [subject]);
        var handler = new CreateSpecializationHandler(db, Actor(tenantId: Tenant), NullLogger<CreateSpecializationHandler>.Instance);

        var result = await handler.Handle(
            new CreateSpecializationCommand(subject.Id, "  Mechanics  "), CancellationToken.None);

        result.Name.Should().Be("Mechanics");
        result.SubjectId.Should().Be(subject.Id);
        result.SubjectName.Should().Be("Physics");
        db.Specializations.Received(1).Add(Arg.Is<Specialization>(s => s.SubjectId == subject.Id));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_not_found_when_subject_missing()
    {
        var handler = new CreateSpecializationHandler(DbWith(), Actor(tenantId: Tenant), NullLogger<CreateSpecializationHandler>.Instance);

        var act = () => handler.Handle(
            new CreateSpecializationCommand(Guid.NewGuid(), "Mechanics"), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Rejects_duplicate_name_within_subject_case_insensitively()
    {
        var subject = Subject.Create(Tenant, "Physics");
        var existing = Specialization.Create(Tenant, subject.Id, "Mechanics");
        var db = DbWith(subjects: [subject], specializations: [existing]);
        var handler = new CreateSpecializationHandler(db, Actor(tenantId: Tenant), NullLogger<CreateSpecializationHandler>.Instance);

        var act = () => handler.Handle(
            new CreateSpecializationCommand(subject.Id, "mechanics"), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ConflictException>();
    }
}
