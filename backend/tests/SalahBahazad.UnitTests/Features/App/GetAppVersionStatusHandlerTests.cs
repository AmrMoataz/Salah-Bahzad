using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Options;
using NSubstitute;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Features.App.Queries.GetAppVersionStatus;

namespace SalahBahazad.UnitTests.Features.App;

/// <summary>
/// <c>GET /api/app/version-status</c> handler (contract §F.1, FR-APP-UPD-001, NFR-APP-UPD-002).
/// Proves the three status values, floor-inclusive semantics, and the two bad-request guards.
/// </summary>
public class GetAppVersionStatusHandlerTests
{
    private static GetAppVersionStatusHandler BuildHandler(
        string minVersion = "1.0.0",
        string latestVersion = "1.0.0",
        string storeUrl = "https://play.google.com/")
    {
        var opts = Substitute.For<IOptionsMonitor<AppVersionsOptions>>();
        opts.CurrentValue.Returns(new AppVersionsOptions
        {
            Platforms = new Dictionary<string, PlatformVersionOptions>
            {
                ["android"] = new() { MinVersion = minVersion, LatestVersion = latestVersion, StoreUrl = storeUrl },
                ["ios"]     = new() { MinVersion = minVersion, LatestVersion = latestVersion, StoreUrl = storeUrl },
                ["windows"] = new() { MinVersion = minVersion, LatestVersion = latestVersion, StoreUrl = storeUrl },
                ["macos"]   = new() { MinVersion = minVersion, LatestVersion = latestVersion, StoreUrl = storeUrl },
            }
        });
        return new GetAppVersionStatusHandler(opts);
    }

    private static GetAppVersionStatusQuery Q(string platform = "android", string version = "1.0.0")
        => new(platform, version);

    // ── Status derivation ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Version_equal_to_min_and_latest_returns_ok()
    {
        var result = await BuildHandler(minVersion: "1.0.0", latestVersion: "1.0.0")
            .Handle(Q("android", "1.0.0"), default);

        result.Status.Should().Be("ok");
        result.MinVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Version_equal_to_min_but_below_latest_returns_update_available()
    {
        var result = await BuildHandler(minVersion: "1.0.0", latestVersion: "1.5.0")
            .Handle(Q("android", "1.0.0"), default);

        result.Status.Should().Be("update_available");
        result.LatestVersion.Should().Be("1.5.0");
    }

    [Fact]
    public async Task Version_equal_to_latest_returns_ok()
    {
        var result = await BuildHandler(minVersion: "1.0.0", latestVersion: "1.5.0")
            .Handle(Q("android", "1.5.0"), default);

        result.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Version_above_latest_returns_ok()
    {
        var result = await BuildHandler(minVersion: "1.0.0", latestVersion: "1.5.0")
            .Handle(Q("android", "2.0.0"), default);

        result.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Version_below_min_returns_update_required()
    {
        var result = await BuildHandler(minVersion: "2.0.0", latestVersion: "2.0.0")
            .Handle(Q("android", "1.0.0"), default);

        result.Status.Should().Be("update_required");
    }

    [Fact]
    public async Task StoreUrl_is_surfaced_in_response()
    {
        var result = await BuildHandler(storeUrl: "https://apps.apple.com/12345")
            .Handle(Q("ios", "1.0.0"), default);

        result.StoreUrl.Should().Be("https://apps.apple.com/12345");
    }

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [InlineData("windows")]
    [InlineData("macos")]
    public async Task All_four_known_platforms_resolve(string platform)
    {
        var result = await BuildHandler().Handle(Q(platform, "1.0.0"), default);
        result.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Platform_lookup_is_case_insensitive()
    {
        // The endpoint lowercases before sending to the handler, but the handler also normalises.
        var result = await BuildHandler().Handle(Q("Android", "1.0.0"), default);
        result.Status.Should().Be("ok");
    }

    // ── Validator (the pipeline runs this before the handler) ─────────────────────────

    [Fact]
    public void Unknown_platform_fails_validation()
    {
        var validator = new GetAppVersionStatusValidator();
        var result = validator.Validate(Q("fridge", "1.0.0"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetAppVersionStatusQuery.Platform));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("abc.def.ghi")]
    public void Unparseable_version_fails_validation(string version)
    {
        var validator = new GetAppVersionStatusValidator();
        var result = validator.Validate(Q("android", version));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetAppVersionStatusQuery.Version));
    }

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [InlineData("windows")]
    [InlineData("macos")]
    [InlineData("Android")]   // case-insensitive
    [InlineData("IOS")]
    public void Known_platforms_pass_validation(string platform)
    {
        var validator = new GetAppVersionStatusValidator();
        var result = validator.Validate(Q(platform, "1.0.0"));
        result.IsValid.Should().BeTrue();
    }
}
