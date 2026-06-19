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
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Infrastructure.Persistence;
using SalahBahazad.Infrastructure.Services;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;

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

    public const string TestBucket = "sb-test-private";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());

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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
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
