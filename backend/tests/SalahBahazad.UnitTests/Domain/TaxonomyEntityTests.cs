using FluentAssertions;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.UnitTests.Domain;

/// <summary>
/// Domain invariants for the taxonomy aggregates and the seeded location reference entities
/// (FR-PLAT-TAX-001/002/003). Factory guards keep an entity from ever being constructed invalid.
/// </summary>
public class TaxonomyEntityTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // ── Grade ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Grade_Create_trims_name_and_sets_tenant()
    {
        var grade = Grade.Create(Tenant, "  Grade 10  ");

        grade.Name.Should().Be("Grade 10");
        grade.TenantId.Should().Be(Tenant);
        grade.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Grade_Create_rejects_blank_name(string? name)
    {
        var act = () => Grade.Create(Tenant, name!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Grade_SoftDelete_stamps_actor_and_time()
    {
        var grade = Grade.Create(Tenant, "Grade 11");
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        grade.SoftDelete(actor, now);

        grade.IsDeleted.Should().BeTrue();
        grade.DeletedById.Should().Be(actor);
        grade.DeletedAtUtc.Should().Be(now);
    }

    // ── Subject ───────────────────────────────────────────────────────────────
    [Fact]
    public void Subject_Rename_trims_and_updates()
    {
        var subject = Subject.Create(Tenant, "Physics");

        subject.Rename("  Chemistry  ");

        subject.Name.Should().Be("Chemistry");
    }

    // ── Specialization ──────────────────────────────────────────────────────────
    [Fact]
    public void Specialization_Create_requires_a_subject()
    {
        var act = () => Specialization.Create(Tenant, Guid.Empty, "Mechanics");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Specialization_Create_sets_owning_subject()
    {
        var subjectId = Guid.NewGuid();

        var specialization = Specialization.Create(Tenant, subjectId, "  Mechanics  ");

        specialization.SubjectId.Should().Be(subjectId);
        specialization.Name.Should().Be("Mechanics");
        specialization.TenantId.Should().Be(Tenant);
    }

    [Fact]
    public void Specialization_Update_can_reassign_subject()
    {
        var specialization = Specialization.Create(Tenant, Guid.NewGuid(), "Mechanics");
        var newSubject = Guid.NewGuid();

        specialization.Update("Thermodynamics", newSubject);

        specialization.Name.Should().Be("Thermodynamics");
        specialization.SubjectId.Should().Be(newSubject);
    }

    // ── City / Region (seeded reference data) ───────────────────────────────────
    [Fact]
    public void City_CreateSeed_uses_the_explicit_id()
    {
        var id = Guid.NewGuid();

        var city = City.CreateSeed(id, "Cairo", "القاهرة");

        city.Id.Should().Be(id);
        city.NameEn.Should().Be("Cairo");
        city.NameAr.Should().Be("القاهرة");
    }

    [Fact]
    public void Region_CreateSeed_requires_a_city()
    {
        var act = () => Region.CreateSeed(Guid.NewGuid(), Guid.Empty, "Maadi", "المعادي");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Region_CreateSeed_links_to_its_city()
    {
        var cityId = Guid.NewGuid();

        var region = Region.CreateSeed(Guid.NewGuid(), cityId, "Maadi", "المعادي");

        region.CityId.Should().Be(cityId);
        region.NameEn.Should().Be("Maadi");
    }
}
