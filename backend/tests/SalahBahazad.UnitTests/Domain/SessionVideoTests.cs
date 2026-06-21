using FluentAssertions;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SessionEntity = SalahBahazad.Domain.Entities.Session;

namespace SalahBahazad.UnitTests.Domain;

public class SessionVideoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static SessionVideo NewVideo()
    {
        var session = SessionEntity.Create(Tenant, "Algebra", null, 100m, 90, Guid.NewGuid(), Guid.NewGuid());
        return session.AddVideo("v", 3, "sessions/t/s/videos/a.mp4");
    }

    [Fact]
    public void New_video_is_pending_with_no_hls_keys()
    {
        var video = NewVideo();

        video.ProcessingStatus.Should().Be(VideoProcessingStatus.Pending);
        video.HlsManifestKey.Should().BeNull();
        video.HlsKeyObjectKey.Should().BeNull();
    }

    [Fact]
    public void MarkReady_sets_both_the_manifest_and_key_object_keys()
    {
        var video = NewVideo();

        video.MarkReady("sessions/t/s/videos/hls/v/index.m3u8", "sessions/t/s/videos/hls/v/enc.key");

        video.ProcessingStatus.Should().Be(VideoProcessingStatus.Ready);
        video.HlsManifestKey.Should().Be("sessions/t/s/videos/hls/v/index.m3u8");
        video.HlsKeyObjectKey.Should().Be("sessions/t/s/videos/hls/v/enc.key");
    }

    [Fact]
    public void MarkFailed_leaves_no_hls_keys()
    {
        var video = NewVideo();

        video.MarkFailed();

        video.ProcessingStatus.Should().Be(VideoProcessingStatus.Failed);
        video.HlsManifestKey.Should().BeNull();
        video.HlsKeyObjectKey.Should().BeNull();
    }
}
