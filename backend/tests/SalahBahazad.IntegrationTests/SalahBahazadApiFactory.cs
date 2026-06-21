using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Persistence;
using SalahBahazad.Infrastructure.Services;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using RealEnrollmentSideEffects = SalahBahazad.Infrastructure.Services.EnrollmentSideEffects;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container (Testcontainers) with Firebase
/// disabled and replaced by a fake. Mints platform JWTs so protected endpoints can be exercised
/// without a real IdP. Shared across the integration suite via <see cref="ApiCollection"/>.
/// </summary>
public sealed class SalahBahazadApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // The test signing material must match what the JWT bearer validation is configured with below.
    public const string JwtSecret = "INTEGRATION-TESTS-ONLY-signing-key-minimum-32-characters!!";
    public const string JwtIssuer = "salah-bahzad-api";
    public const string JwtAudience = "salah-bahzad-admin";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // MinIO emulates Cloudflare R2 so the IFileStorage → R2FileStorage path is exercised offline.
    private readonly MinioContainer _minio = new MinioBuilder().Build();

    // Redis backs the SignalR backplane, the QuizHub connection map, and the HybridCache L2 (Phase 5B-2).
    private readonly RedisContainer _redis = new RedisBuilder().Build();

    public const string TestBucket = "sb-test-private";

    /// <summary>Records enrollment side-effect invocations so tests can prove the enrollment event fired.</summary>
    public SpyEnrollmentSideEffects EnrollmentSideEffects { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync(), _redis.StartAsync());

        // Touching Services builds the host (running ConfigureWebHost, which needs the started
        // containers' connection details), then we apply the real migrations and create the bucket.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await ObjectStorageInitializer.EnsureBucketsAsync(Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", _redis.GetConnectionString());
        builder.UseSetting("Firebase:Disable", "true");
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);

        // Point object storage at the MinIO container (the real R2FileStorage path).
        builder.UseSetting("R2:Endpoint", _minio.GetConnectionString());
        builder.UseSetting("R2:AccessKeyId", _minio.GetAccessKey());
        builder.UseSetting("R2:SecretAccessKey", _minio.GetSecretKey());
        builder.UseSetting("R2:BucketPrivate", TestBucket);
        builder.UseSetting("R2:SignedUrlTtlSeconds", "120");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFirebaseAuthService>();
            services.AddSingleton<IFirebaseAuthService, FakeFirebaseAuthService>();

            // Run the REAL assignment-generation side-effect (Phase 5B-1) but record each invocation so the
            // existing "the enrollment event fired" assertions keep working. Scoped so the real service gets the
            // request-scoped DbContext + tenant/audit context (System-attributed AssignmentGenerated row).
            services.RemoveAll<IEnrollmentSideEffects>();
            services.AddScoped<RealEnrollmentSideEffects>();
            services.AddScoped<IEnrollmentSideEffects>(sp =>
                new RecordingEnrollmentSideEffects(
                    sp.GetRequiredService<RealEnrollmentSideEffects>(), EnrollmentSideEffects));

            // Video transcode (Phase 5C): fake ffmpeg (no binary in CI) and run the job inline so a video is
            // Ready synchronously — matching the Phase-3 stub timing the content tests rely on. Real ffmpeg +
            // Hangfire are proven in live wiring + the opt-in real-ffmpeg test.
            services.RemoveAll<IMediaTranscoder>();
            services.AddSingleton<IMediaTranscoder, FakeMediaTranscoder>();
            services.RemoveAll<IVideoProcessingQueue>();
            services.AddScoped<IVideoProcessingQueue, InlineVideoProcessingQueue>();
        });
    }

    // ── Auth helpers ─────────────────────────────────────────────────────────
    public string CreateToken(StaffRole role, Guid tenantId, Guid staffId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staffId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role.ToString()),
            new("token_type", "access"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public HttpClient CreateClientFor(StaffRole role, Guid tenantId, Guid? staffId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(role, tenantId, staffId ?? Guid.NewGuid()));
        return client;
    }

    /// <summary>Mints a Student-role platform JWT (role claim "Student") for the redeem path (#12).</summary>
    public string CreateStudentToken(Guid tenantId, Guid studentId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, studentId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, "Student"),
            new("token_type", "access"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public HttpClient CreateClientForStudent(Guid tenantId, Guid studentId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateStudentToken(tenantId, studentId));
        return client;
    }

    /// <summary>Runs a read against a fresh DbContext scope (no HTTP context → use IgnoreQueryFilters in tenant
    /// queries). For asserting persisted state the API does not surface (code status, counters, payments).</summary>
    public async Task<T> QueryDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────
    // No HTTP context in these scopes → the tenant resolver yields Guid.Empty, so the audit
    // interceptor is a no-op and seeding never writes spurious AuditEntry rows.
    public async Task<Guid> SeedTenantAsync() => (await SeedTenantEntityAsync()).Id;

    /// <summary>Seeds a tenant and returns the entity (callers needing the slug, e.g. registration).</summary>
    public async Task<Tenant> SeedTenantEntityAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = Tenant.Create($"Tenant {Guid.NewGuid():N}", $"t-{Guid.NewGuid():N}");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    public async Task<Grade> SeedGradeAsync(Guid tenantId, string? name = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grade = Grade.Create(tenantId, name ?? $"Grade {Guid.NewGuid():N}");
        db.Grades.Add(grade);
        await db.SaveChangesAsync();
        return grade;
    }

    /// <summary>Returns a seeded (global) city + one of its regions from the Egypt reference dataset.</summary>
    public async Task<(Guid CityId, Guid RegionId)> GetSeedLocationAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var region = await db.Regions.AsNoTracking().FirstAsync();
        return (region.CityId, region.Id);
    }

    public async Task<Student> SeedStudentAsync(
        Guid tenantId,
        Guid gradeId,
        Guid cityId,
        Guid regionId,
        StudentStatus status = StudentStatus.Pending,
        string? idImageKey = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var student = Student.Register(
            tenantId, $"uid-{Guid.NewGuid():N}", "Seed Student", "01055555555", "01000000000", null,
            gradeId, cityId, regionId, "Seed School", "v1", DateTimeOffset.UtcNow);

        switch (status)
        {
            case StudentStatus.Active:
                student.Approve();
                break;
            case StudentStatus.Rejected:
                student.Reject("seeded rejection");
                break;
            case StudentStatus.Inactive:
                student.Approve();
                student.Deactivate();
                break;
        }

        if (idImageKey is not null)
            student.AttachIdImage(idImageKey);

        db.Students.Add(student);
        await db.SaveChangesAsync(); // no HTTP context → tenant resolves to Empty → no audit rows
        return student;
    }

    public async Task<StudentDevice> SeedDeviceAsync(Guid tenantId, Guid studentId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = StudentDevice.Bind(
            tenantId, studentId, $"hash-{Guid.NewGuid():N}", "iOS 18 · Safari", DateTimeOffset.UtcNow);
        db.StudentDevices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    public async Task<Staff> SeedStaffAsync(Guid tenantId, StaffRole role, string? email = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staff = Staff.Create(
            tenantId,
            $"uid-{Guid.NewGuid():N}",
            "Seeded Staff",
            email ?? $"seed-{Guid.NewGuid():N}@example.com",
            role);
        db.Staff.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }

    public async Task<Subject> SeedSubjectAsync(Guid tenantId, string? name = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = Subject.Create(tenantId, name ?? $"Subject {Guid.NewGuid():N}");
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return subject;
    }

    public async Task<Specialization> SeedSpecializationAsync(Guid tenantId, Guid subjectId, string? name = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var specialization = Specialization.Create(tenantId, subjectId, name ?? $"Spec {Guid.NewGuid():N}");
        db.Specializations.Add(specialization);
        await db.SaveChangesAsync();
        return specialization;
    }

    /// <summary>Seeds a grade + subject + specialization for a tenant and returns their ids.</summary>
    public async Task<(Guid GradeId, Guid SubjectId, Guid SpecializationId)> SeedTaxonomyAsync(Guid tenantId)
    {
        var grade = await SeedGradeAsync(tenantId);
        var subject = await SeedSubjectAsync(tenantId);
        var specialization = await SeedSpecializationAsync(tenantId, subject.Id);
        return (grade.Id, subject.Id, specialization.Id);
    }

    public async Task<Session> SeedSessionAsync(
        Guid tenantId,
        Guid gradeId,
        Guid specializationId,
        SessionStatus status = SessionStatus.Draft,
        string? title = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = Session.Create(
            tenantId, title ?? $"Session {Guid.NewGuid():N}", "Seeded session", 100m, 90, gradeId, specializationId);

        switch (status)
        {
            case SessionStatus.Published:
                session.Publish();
                break;
            case SessionStatus.Archived:
                session.Archive();
                break;
        }

        db.Sessions.Add(session);
        await db.SaveChangesAsync(); // no HTTP context → tenant resolves to Empty → no audit rows
        return session;
    }

    /// <summary>Seeds a (by default Published) session with videos and a known price — for enrollment tests
    /// that need per-video access counters provisioned and a price to value-match a code against.</summary>
    public async Task<Session> SeedSessionWithContentAsync(
        Guid tenantId,
        Guid gradeId,
        Guid specializationId,
        decimal price = 100m,
        int validityDays = 90,
        SessionStatus status = SessionStatus.Published,
        int videoCount = 2,
        int accessPerVideo = 3)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = Session.Create(
            tenantId, $"Session {Guid.NewGuid():N}", "Seeded session", price, validityDays, gradeId, specializationId);

        for (var i = 0; i < videoCount; i++)
            session.AddVideo($"Lesson {i + 1}", accessPerVideo, $"sessions/{tenantId}/videos/{Guid.NewGuid():N}.mp4");

        if (status == SessionStatus.Published)
            session.Publish();

        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>
    /// Marks a seeded (Pending) video Ready and uploads a tiny encrypted-HLS set (manifest + one segment + the
    /// 16-byte key) to MinIO under the video's HLS prefix — so the playback gate, redeem (signed segment URL),
    /// and key endpoint resolve against real storage without going through the upload/transcode flow.
    /// </summary>
    public async Task SeedReadyHlsAsync(Guid videoId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var video = await db.SessionVideos.FirstAsync(v => v.Id == videoId);
        var prefix = HlsConventions.HlsPrefix(video.SourceObjectKey, videoId);
        var manifestKey = HlsConventions.ManifestKey(prefix);
        var keyObjectKey = HlsConventions.KeyObjectKey(prefix);
        var segmentKey = HlsConventions.SegmentKey(manifestKey, "seg_000.ts");

        await storage.UploadPrivateAsync(
            manifestKey,
            new MemoryStream(Encoding.UTF8.GetBytes(FakeMediaTranscoder.BuildManifest())),
            "application/vnd.apple.mpegurl");
        await storage.UploadPrivateAsync(segmentKey, new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]), "video/mp2t");
        await storage.UploadPrivateAsync(keyObjectKey, new MemoryStream(FakeMediaTranscoder.Key), "application/octet-stream");

        video.MarkReady(manifestKey, keyObjectKey);
        await db.SaveChangesAsync();
    }

    public async Task<Question> SeedQuestionAsync(
        Guid tenantId, Guid sessionId, bool isValidForQuiz = true, string? bodyLatex = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var question = Question.Create(
            tenantId, sessionId, bodyLatex ?? "x^2 + 1", 1, isValidForQuiz, null,
            [new QuestionOptionDraft("A", true), new QuestionOptionDraft("B", false)]);
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return question;
    }

    /// <summary>
    /// Seeds a published session (with videos) plus <paramref name="questionCount"/> MCQ questions — each with
    /// option "A" correct and "B" wrong, mark 1 — for the assignment engine + gate tests (Phase 5B-1).
    /// </summary>
    public async Task<Session> SeedSessionWithQuestionsAsync(
        Guid tenantId,
        Guid gradeId,
        Guid specializationId,
        int questionCount = 2,
        decimal price = 100m,
        int validityDays = 90)
    {
        var session = await SeedSessionWithContentAsync(
            tenantId, gradeId, specializationId, price, validityDays);
        for (var i = 0; i < questionCount; i++)
            await SeedQuestionAsync(tenantId, session.Id);
        return session;
    }

    /// <summary>Sets a session's prerequisite directly (no HTTP context → no audit), for the ENR-007 gate tests.</summary>
    public async Task SetSessionPrerequisiteAsync(Guid sessionId, Guid prerequisiteSessionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.Sessions.IgnoreQueryFilters().FirstAsync(s => s.Id == sessionId);
        session.SetPrerequisite(prerequisiteSessionId);
        await db.SaveChangesAsync();
    }

    /// <summary>Sets a session's gating-quiz settings directly (no HTTP context → no audit), for the 5B-2 tests.</summary>
    public async Task SetQuizSettingsAsync(
        Guid sessionId, int timeLimitMinutes, int questionCount, int attemptCount, int minPassPercent)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.Sessions.IgnoreQueryFilters().FirstAsync(s => s.Id == sessionId);
        session.UpdateQuizSettings(timeLimitMinutes, questionCount, attemptCount, minPassPercent);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds an <see cref="AuditEntry"/> directly for a tenant (no HTTP context → the audit interceptor is a
    /// no-op, so this is the only row written, with exactly the tenant/actor/action/time given). Used by the
    /// Phase-5A audit-feed tests to control isolation, sensitive scoping, filters and ordering deterministically.
    /// </summary>
    public async Task<AuditEntry> SeedAuditAsync(
        Guid tenantId,
        string action,
        string entityType = "Student",
        Guid? entityId = null,
        string actorType = "Staff",
        Guid? actorId = null,
        string? actorRole = "Teacher",
        DateTimeOffset? occurredAtUtc = null,
        string? summary = null,
        string? portal = "admin",
        string? ipAddress = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = AuditEntry.Create(
            tenantId: tenantId,
            action: action,
            entityType: entityType,
            occurredAtUtc: occurredAtUtc ?? DateTimeOffset.UtcNow,
            entityId: entityId,
            actorId: actorId,
            actorRole: actorRole,
            actorType: actorType,
            summary: summary ?? $"{action} {entityType}",
            portal: portal,
            ipAddress: ipAddress);
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task<AuditEntry?> LatestStaffAuditAsync(Guid tenantId, string action)
        => await LatestAuditAsync(tenantId, nameof(Staff), action);

    /// <summary>Most recent audit entry of a given entity type + action within a tenant (most recent Id wins).</summary>
    public async Task<AuditEntry?> LatestAuditAsync(Guid tenantId, string entityType, string action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditEntries
            .Where(a => a.TenantId == tenantId && a.EntityType == entityType && a.Action == action)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
    }
}
