using FluentAssertions;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;
using SessionEntity = SalahBahazad.Domain.Entities.Session;

namespace SalahBahazad.UnitTests.Domain;

public class SessionTests
{
    private static SessionEntity NewDraft(string title = "  Algebra I  ", string? description = "  Intro  ") =>
        SessionEntity.Create(
            tenantId: Guid.NewGuid(),
            title: title,
            description: description,
            price: 100m,
            validityDays: 90,
            gradeId: Guid.NewGuid(),
            specializationId: Guid.NewGuid());

    [Fact]
    public void Create_starts_draft_trims_and_raises_event()
    {
        var session = NewDraft();

        session.Status.Should().Be(SessionStatus.Draft);
        session.Title.Should().Be("Algebra I");
        session.Description.Should().Be("Intro");
        session.PrerequisiteSessionId.Should().BeNull();
        session.QuizSetting.Should().BeNull();
        session.IsDeleted.Should().BeFalse();
        session.DomainEvents.OfType<SessionCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_blank_description_becomes_null()
        => NewDraft(description: "   ").Description.Should().BeNull();

    [Theory]
    [InlineData(0)]
    [InlineData(365)]
    public void Create_accepts_validity_bounds(int days)
    {
        var act = () => SessionEntity.Create(Guid.NewGuid(), "T", null, 0m, days, Guid.NewGuid(), Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Create_rejects_validity_out_of_range(int days)
    {
        var act = () => SessionEntity.Create(Guid.NewGuid(), "T", null, 0m, days, Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_negative_price()
    {
        var act = () => SessionEntity.Create(Guid.NewGuid(), "T", null, -1m, 30, Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_requires_grade_and_specialization()
    {
        var noGrade = () => SessionEntity.Create(Guid.NewGuid(), "T", null, 0m, 30, Guid.Empty, Guid.NewGuid());
        var noSpec = () => SessionEntity.Create(Guid.NewGuid(), "T", null, 0m, 30, Guid.NewGuid(), Guid.Empty);

        noGrade.Should().Throw<ArgumentException>();
        noSpec.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetPrerequisite_blocks_direct_self_reference()
    {
        var session = NewDraft();
        var act = () => session.SetPrerequisite(session.Id);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetPrerequisite_sets_and_clears()
    {
        var session = NewDraft();
        var other = Guid.NewGuid();

        session.SetPrerequisite(other);
        session.PrerequisiteSessionId.Should().Be(other);

        session.SetPrerequisite(null);
        session.PrerequisiteSessionId.Should().BeNull();
    }

    [Fact]
    public void UpdateQuizSettings_sets_owned_value()
    {
        var session = NewDraft();
        session.UpdateQuizSettings(15, 10, 2, 60);

        session.QuizSetting.Should().NotBeNull();
        session.QuizSetting!.TimeLimitMinutes.Should().Be(15);
        session.QuizSetting.QuestionCount.Should().Be(10);
        session.QuizSetting.AttemptCount.Should().Be(2);
        session.QuizSetting.MinPassPercent.Should().Be(60);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateQuizSettings_rejects_pass_percent_out_of_range(int pass)
    {
        var session = NewDraft();
        var act = () => session.UpdateQuizSettings(15, 10, 2, pass);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Publish_then_archive_transitions_and_raises_events()
    {
        var session = NewDraft();

        session.Publish();
        session.Status.Should().Be(SessionStatus.Published);
        session.DomainEvents.OfType<SessionPublishedEvent>().Should().ContainSingle();

        session.Archive();
        session.Status.Should().Be(SessionStatus.Archived);
        session.DomainEvents.OfType<SessionArchivedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Publish_twice_is_illegal()
    {
        var session = NewDraft();
        session.Publish();
        var act = () => session.Publish();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_twice_is_illegal()
    {
        var session = NewDraft();
        session.Archive();
        var act = () => session.Archive();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SoftDelete_sets_attribution_and_raises_event()
    {
        var session = NewDraft();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        session.SoftDelete(actor, now);

        session.IsDeleted.Should().BeTrue();
        session.DeletedById.Should().Be(actor);
        session.DeletedAtUtc.Should().Be(now);
        session.DomainEvents.OfType<SessionDeletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void AddVideo_appends_in_order_as_pending()
    {
        var session = NewDraft();

        var v1 = session.AddVideo("Intro", 8, 3, "sessions/t/videos/a.mp4");
        var v2 = session.AddVideo("Part 2", 12, 3, "sessions/t/videos/b.mp4");

        session.Videos.Should().HaveCount(2);
        v1.Order.Should().Be(0);
        v2.Order.Should().Be(1);
        v1.ProcessingStatus.Should().Be(VideoProcessingStatus.Pending);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(8, -1)]
    public void AddVideo_rejects_negative_length_or_access_count(int length, int accessCount)
    {
        var session = NewDraft();
        var act = () => session.AddVideo("V", length, accessCount, "k");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateVideo_replacing_source_resets_to_pending()
    {
        var session = NewDraft();
        var video = session.AddVideo("V", 8, 3, "k1");
        video.MarkReady();

        session.UpdateVideo(video.Id, "V2", 9, 5, "k2");

        video.Title.Should().Be("V2");
        video.LengthMinutes.Should().Be(9);
        video.AccessCount.Should().Be(5);
        video.SourceObjectKey.Should().Be("k2");
        video.ProcessingStatus.Should().Be(VideoProcessingStatus.Pending);
    }

    [Fact]
    public void UpdateVideo_metadata_only_keeps_status_and_source()
    {
        var session = NewDraft();
        var video = session.AddVideo("V", 8, 3, "k1");
        video.MarkReady();

        session.UpdateVideo(video.Id, "V2", 9, 5, newSourceObjectKey: null);

        video.SourceObjectKey.Should().Be("k1");
        video.ProcessingStatus.Should().Be(VideoProcessingStatus.Ready);
    }

    [Fact]
    public void ReorderVideos_reassigns_order()
    {
        var session = NewDraft();
        var a = session.AddVideo("a", 1, 1, "ka");
        var b = session.AddVideo("b", 1, 1, "kb");
        var c = session.AddVideo("c", 1, 1, "kc");

        session.ReorderVideos([c.Id, a.Id, b.Id]);

        c.Order.Should().Be(0);
        a.Order.Should().Be(1);
        b.Order.Should().Be(2);
    }

    [Fact]
    public void ReorderVideos_rejects_mismatched_set()
    {
        var session = NewDraft();
        var a = session.AddVideo("a", 1, 1, "ka");
        session.AddVideo("b", 1, 1, "kb");

        var act = () => session.ReorderVideos([a.Id]); // missing one
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveVideo_unknown_throws()
    {
        var session = NewDraft();
        var act = () => session.RemoveVideo(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddAndRemoveMaterial_roundtrips()
    {
        var session = NewDraft();
        var material = session.AddMaterial("notes.pdf", "application/pdf", "sessions/t/materials/x.pdf", 1234);

        session.Materials.Should().ContainSingle();
        session.RemoveMaterial(material.Id);
        session.Materials.Should().BeEmpty();
    }
}
