using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Taxonomy.Subjects.Commands.DeleteSubject;
using SalahBahazad.Domain.Entities;
using static SalahBahazad.UnitTests.Features.Taxonomy.TaxonomyTestHelpers;

namespace SalahBahazad.UnitTests.Features.Taxonomy;

/// <summary>
/// Delete-in-use rule for subjects (FR-PLAT-TAX-004): a subject that still has live specializations
/// cannot be deleted; one with none is soft-deleted (FR-PLAT-ROLE-004).
/// </summary>
public class DeleteSubjectHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Blocks_delete_when_subject_has_specializations()
    {
        var subject = Subject.Create(Tenant, "Physics");
        var specialization = Specialization.Create(Tenant, subject.Id, "Mechanics");
        var db = DbWith(subjects: [subject], specializations: [specialization]);
        var handler = new DeleteSubjectHandler(db, TimeProvider.System, Actor(tenantId: Tenant), NullLogger<DeleteSubjectHandler>.Instance);

        var act = () => handler.Handle(new DeleteSubjectCommand(subject.Id), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ConflictException>();
        subject.IsDeleted.Should().BeFalse();
        await db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Soft_deletes_when_no_specializations()
    {
        var subject = Subject.Create(Tenant, "Physics");
        var actor = Guid.NewGuid();
        var db = DbWith(subjects: [subject]);
        var handler = new DeleteSubjectHandler(db, TimeProvider.System, Actor(actor, Tenant), NullLogger<DeleteSubjectHandler>.Instance);

        await handler.Handle(new DeleteSubjectCommand(subject.Id), CancellationToken.None);

        subject.IsDeleted.Should().BeTrue();
        subject.DeletedById.Should().Be(actor);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_not_found_for_unknown_subject()
    {
        var handler = new DeleteSubjectHandler(DbWith(), TimeProvider.System, Actor(tenantId: Tenant), NullLogger<DeleteSubjectHandler>.Instance);

        var act = () => handler.Handle(new DeleteSubjectCommand(Guid.NewGuid()), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
