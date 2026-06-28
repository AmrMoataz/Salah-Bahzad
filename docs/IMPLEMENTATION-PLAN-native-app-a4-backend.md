# Native App · A4 — BACKEND stream (min-version: `GET /api/app/version-status` + `426 outdated_app` at `redeem`)

> Status: **Planned — not yet built** · Created 2026-06-28 · The **engine half** of phase **A4** in `docs/IMPLEMENTATION-PLAN-native-app.md` (§A4). This stream adds exactly two things: a new **`GET /api/app/version-status`** endpoint (anonymous, for the app's launch check) and **`X-App-Version`/`X-Platform` enforcement** at `POST /api/me/videos/playback/redeem` → `426 Upgrade Required` (`reason: outdated_app`). Both are config-driven with **no migration** and hot-reloadable via `IOptionsMonitor<AppVersionsOptions>`.
>
> Satisfies: `FR-APP-UPD-001`, `NFR-APP-UPD-002`. Implements **`docs/contracts/native-app-playback.md` §F** verbatim. **Change the contract first if anything moves.**
>
> Run in its own session, parallel with the app stream. **File ownership: `backend/**` only.** Gate: `dotnet test -c Release` green (including new integration tests).

---

## Design reference

No UI. This stream owns two wire-level contract points consumed by the app on every launch (`version-status`) and every redeem (`X-App-Version`/`X-Platform` header check). Behaviour authority: contract §F + `FR-APP-UPD-001` + `NFR-APP-UPD-002`. The app side renders the `update-required` §H state; the backend only returns `426 outdated_app` and the store URL — the app decides what to render.

## 1. Frozen contract (this stream)

Implements `docs/contracts/native-app-playback.md`:

**§F.1 — `GET /api/app/version-status?platform={p}&version={v}` (`AllowAnonymous`) · `200`:**
```jsonc
{ "status": "ok",                  // "ok" | "update_available" | "update_required"
  "minVersion": "1.4.0",           // from config — the floor for this platform
  "latestVersion": "1.6.0",        // from config — the latest shipped
  "storeUrl": "https://…" }        // the store/download URL for {platform}
```
- `platform` ∈ `android | ios | windows | macos` (case-insensitive in the query; normalised to lower).
- `status` derivation: `version < minVersion` → `update_required`; `version >= minVersion && version < latestVersion` → `update_available`; `version >= latestVersion` → `ok`.
- Missing / unrecognised `platform` → `400 Bad Request`. Missing / unparseable `version` → `400 Bad Request`. All errors as `ProblemDetails`.
- **Reads not audited** (contract §0).

**§F.2 — Hard enforcement at `POST /api/me/videos/playback/redeem` (EXISTING endpoint, `RequireStudent`):**
- Read `X-App-Version` and `X-Platform` headers from the request.
- If either header is absent, or `X-Platform` is not a recognised value, or `X-App-Version` is not parseable as `SemVer`, or `X-App-Version < minVersion[X-Platform]` → **`426 Upgrade Required`**, body `ProblemDetails` with `reason: outdated_app`, `detail` carrying the store URL.
- The existing `POST /api/me/videos/{videoId}/playback` (the gate, called by the **browser portal**) is **not touched** — versioning only on `redeem` (the app-only step).

## 2. Pre-flight — confirm what already EXISTS (do not rebuild)

Read `backend/CLAUDE.md` + master plan §A4. Then confirm in code:
- **`VideoEndpoints.cs:34-65`** — `RedeemAsync` exists; it reads `HandoffCode` from the body + `apiBaseUrl` from `HttpContext`, sends `RedeemPlaybackQuery`, returns `200 PlaybackManifestDto`. **Does not yet read version headers** — that is A4's only change to this file.
- **`RedeemPlaybackQuery.cs`** + **`RedeemPlaybackHandler.cs`** — locate in `Application/Features/Videos/Queries/RedeemPlayback/`. The handler returns `PlaybackManifestDto`; it throws `AppException.Gone("handoff_expired")` / `AppException.NotFound()` / `AppException.Forbidden("not_enrolled")` etc. Do not change the handler — the version check is a **middleware/endpoint responsibility**, not a domain rule.
- **`IOptionsMonitor<T>` precedent** — search for existing `IOptions<T>` or `IOptionsMonitor<T>` usage in `Infrastructure/`. Mirror the registration pattern exactly.
- **`AllowAnonymous` endpoint precedent** — `AuthEndpoints.cs` (`app-exchange`, `refresh`) for the `MapGroup` + `.AllowAnonymous()` + rate-limit pattern. The `version-status` endpoint is read-only with no auth and no rate limit needed (it's a free check, the floor for abuse is the server's global throughput limit).
- **`ProblemDetails` + `reason` extension precedent** — `AppException.cs` in `Application/Common/` (or equivalent). The `426 outdated_app` response follows the same `{ reason: "outdated_app", detail: "<store URL>" }` shape used by `410 handoff_expired` etc.
- **No migration** — `AppVersionsOptions` is config-only, not persisted to the database.

## 3. Infrastructure — `AppVersionsOptions` (config-driven, hot-reloadable)

**3.1 New class — `SalahBahazad.Application/Common/AppVersionsOptions.cs`** (or `Infrastructure/Options/` — follow the project's existing options convention):
```csharp
/// <summary>Per-platform minimum/latest app versions. Hot-reloadable via IOptionsMonitor.</summary>
public sealed class AppVersionsOptions
{
    public const string SectionName = "AppVersions";

    public AppVersionsOptions() { }

    /// <summary>Platforms keyed by their canonical lowercase name (android | ios | windows | macos).</summary>
    public Dictionary<string, PlatformVersionOptions> Platforms { get; init; } = new();
}

public sealed class PlatformVersionOptions
{
    public string MinVersion { get; init; } = "0.0.0";
    public string LatestVersion { get; init; } = "0.0.0";
    public string StoreUrl { get; init; } = string.Empty;
}
```

**3.2 `appsettings.json` — new section (no secrets, safe to commit):**
```json
"AppVersions": {
  "Platforms": {
    "android":  { "MinVersion": "1.0.0", "LatestVersion": "1.0.0", "StoreUrl": "" },
    "ios":      { "MinVersion": "1.0.0", "LatestVersion": "1.0.0", "StoreUrl": "" },
    "windows":  { "MinVersion": "1.0.0", "LatestVersion": "1.0.0", "StoreUrl": "" },
    "macos":    { "MinVersion": "1.0.0", "LatestVersion": "1.0.0", "StoreUrl": "" }
  }
}
```
- Floor is `1.0.0` (first shipped build). Operator raises the floor by editing `appsettings.{Environment}.json` or via environment variables — **no redeploy** (hot-reload via `IOptionsMonitor`).
- Store URLs are empty strings for v1 (the app shows the update-required screen and the user navigates manually). Populate when real store URLs are known.

**3.3 DI registration — `Program.cs` or the infrastructure extension:**
```csharp
services.Configure<AppVersionsOptions>(configuration.GetSection(AppVersionsOptions.SectionName));
```
`IOptionsMonitor<AppVersionsOptions>` is automatically available after this.

## 4. Application — `GetAppVersionStatusQuery` + handler

**4.1 New query — `Application/Features/App/Queries/GetAppVersionStatus/`:**
```csharp
public sealed record GetAppVersionStatusQuery(string Platform, string Version) : IRequest<AppVersionStatusDto>;
```

**4.2 DTO — `AppVersionStatusDto` (in the same folder or `Application/Features/App/DTOs/`):**
```csharp
public sealed record AppVersionStatusDto(
    string Status,         // "ok" | "update_available" | "update_required"
    string MinVersion,
    string LatestVersion,
    string StoreUrl);
```

**4.3 Handler — `GetAppVersionStatusHandler.cs`:**
```csharp
public sealed class GetAppVersionStatusHandler : IRequestHandler<GetAppVersionStatusQuery, AppVersionStatusDto>
{
    private readonly IOptionsMonitor<AppVersionsOptions> _opts;

    public GetAppVersionStatusHandler(IOptionsMonitor<AppVersionsOptions> opts) => _opts = opts;

    public ValueTask<AppVersionStatusDto> Handle(GetAppVersionStatusQuery query, CancellationToken ct)
    {
        var platform = query.Platform.Trim().ToLowerInvariant();
        var opts = _opts.CurrentValue;

        if (!opts.Platforms.TryGetValue(platform, out var pOpts))
            throw new AppException(StatusCodes.Status400BadRequest, "unknown_platform",
                $"Unknown platform '{query.Platform}'. Must be android | ios | windows | macos.");

        if (!Version.TryParse(query.Version, out var requested))
            throw new AppException(StatusCodes.Status400BadRequest, "invalid_version",
                $"Version '{query.Version}' is not a valid semver string.");

        var min = Version.Parse(pOpts.MinVersion);
        var latest = Version.Parse(pOpts.LatestVersion);

        var status = requested < min ? "update_required"
                   : requested < latest ? "update_available"
                   : "ok";

        return ValueTask.FromResult(new AppVersionStatusDto(status, pOpts.MinVersion, pOpts.LatestVersion, pOpts.StoreUrl));
    }
}
```
> Version comparison uses `System.Version` (major.minor.patch), which is sufficient for semver comparisons without a pre-release suffix. If pre-release versions are needed in future, swap for a proper semver library — flag this in a comment.

**No FluentValidation validator** — the handler validates inline (two cheap guards, no async I/O). Follow the project convention: if the project uses a validation pipeline behaviour for all queries, add `GetAppVersionStatusValidator` with the same two checks there; if the convention is handler-inline, keep it in the handler.

## 5. API — `AppEndpoints.cs` (new endpoint group)

**New file — `Api/Endpoints/AppEndpoints.cs`:**
```csharp
internal sealed class AppEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app")
            .WithTags("App")
            .WithOpenApi()
            .AllowAnonymous();

        group.MapGet("/version-status", GetVersionStatusAsync)
            .WithName("GetAppVersionStatus")
            .WithSummary("Returns whether the calling app version is current, updatable, or below the minimum floor")
            .Produces<AppVersionStatusDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetVersionStatusAsync(
        [AsParameters] AppVersionStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetAppVersionStatusQuery(request.Platform, request.Version), cancellationToken));
}

internal sealed record AppVersionStatusRequest(
    [FromQuery] string Platform,
    [FromQuery] string Version);
```
- Register `AppEndpoints` in `EndpointExtensions.cs` (or wherever the project assembles endpoint groups).
- No auth, no rate limit.

## 6. Redeem — min-version enforcement (endpoint change only)

**Modified `VideoEndpoints.cs` — `RedeemAsync`:**

Read the two headers and compare against the config before forwarding to the handler. The comparison is the same `requested < min` logic as the handler above, reusing `IOptionsMonitor<AppVersionsOptions>` injected into the endpoint or resolved from `HttpContext.RequestServices`.

```csharp
private static async Task<IResult> RedeemAsync(
    [FromBody] RedeemPlaybackRequest request,
    HttpContext httpContext,
    IOptionsMonitor<AppVersionsOptions> versionOptions,
    ISender sender,
    CancellationToken cancellationToken)
{
    // Min-version gate (contract §F.2) — app-only step; portal gate (StartPlayback) is unaffected.
    var platform = httpContext.Request.Headers["X-Platform"].FirstOrDefault()?.Trim().ToLowerInvariant();
    var versionRaw = httpContext.Request.Headers["X-App-Version"].FirstOrDefault();
    var opts = versionOptions.CurrentValue;

    if (platform is not null && opts.Platforms.TryGetValue(platform, out var pOpts) &&
        Version.TryParse(versionRaw, out var requested) &&
        Version.TryParse(pOpts.MinVersion, out var min) &&
        requested < min)
    {
        return Results.Problem(
            detail: pOpts.StoreUrl,
            statusCode: StatusCodes.Status426UpgradeRequired,
            extensions: new Dictionary<string, object?> { ["reason"] = "outdated_app" });
    }

    var apiBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    return Results.Ok(await sender.Send(
        new RedeemPlaybackQuery(request.HandoffCode, apiBaseUrl), cancellationToken));
}
```

> **Leniency rule (deliberate):** if either header is absent or the platform is unrecognised, the check is skipped — the gate proceeds normally. This prevents accidentally blocking the browser portal (which never sends these headers) or blocking future platforms before their config is added. The "strict" path (absent → `400`) is documented as a future tightening option; for v1, leniency is the safe default.
>
> If the project convention is to keep endpoint methods thin and push logic into a service/handler, extract the version check into `IAppVersionChecker` — but do not push it into `RedeemPlaybackHandler` (that is a domain handler; version enforcement is an API-layer concern).

## 7. Tests

**7.1 Unit — `SalahBahazad.UnitTests/Application/GetAppVersionStatusHandlerTests.cs`:**
Six cases covering the three status values × a valid platform + edge cases:
- `version == minVersion` → `ok` (floor is inclusive).
- `version > minVersion && version < latestVersion` → `update_available`.
- `version >= latestVersion` → `ok`.
- `version < minVersion` → `update_required`.
- Unknown `platform` → `AppException` with status 400.
- Unparseable `version` → `AppException` with status 400.

**7.2 Integration — `SalahBahazad.IntegrationTests/App/`:**

`GetAppVersionStatusTests.cs`:
- `GET /api/app/version-status?platform=android&version=1.0.0` with config floor `1.0.0` → `200 { status: "ok" }`.
- `version=0.9.0` with floor `1.0.0` → `200 { status: "update_required" }`.
- Unknown `platform=fridge` → `400`.
- Missing `version` param → `400`.
- No JWT needed (anonymous).

`RedeemVersionEnforcementTests.cs`:
- Valid student JWT + `X-App-Version: 0.9.0` + `X-Platform: android` + floor `1.0.0` on the handoff that was just minted → `426 { reason: "outdated_app" }`.
- Same but `X-App-Version: 1.0.0` (at the floor) → `200 PlaybackManifestDto` (not rejected).
- No `X-App-Version`/`X-Platform` headers → `200` (leniency rule — don't block portal).
- Confirm the gate (`StartVideoPlayback`) is **unaffected** by version headers — it ignores them.

> Both new test classes follow the project's `WebApplicationFactory` + Testcontainers pattern. The integration factory seeds `AppVersions` options via the test host's `WithWebHostBuilder` override or an `appsettings.Testing.json`.

## 8. Green gate

```bash
dotnet build -c Release   # zero warnings
dotnet test -c Release    # all pass; pre-existing QuestionBank image-test is the known baseline
```

Run the integration tests locally first (`dotnet test --filter Integration`). The new `AppVersions` config section defaults (`1.0.0` / `1.0.0`) result in `status: ok` for any `1.0.0` app — safe to deploy before the app ships.

---

## Deferred

- **Admin UI for managing version floors** — a DB-backed setting and an admin endpoint. Out of v1 scope; the current config-file approach is sufficient and hot-reloadable.
- **Rate-limiting `version-status`** — the endpoint is read-only and anonymous but called once per app launch. A global throughput limit is sufficient for v1; a dedicated bucket can be added later.
- **Strict enforcement on absent `X-App-Version`** (`absent → 400`) — deferred; v1 leniency prevents breaking the portal.
