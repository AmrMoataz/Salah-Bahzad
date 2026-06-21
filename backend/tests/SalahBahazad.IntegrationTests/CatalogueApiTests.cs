using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// The student-portal catalogue read (S2, contract §A/§C, FR-STU-CAT-001/002/004): published-only + DESC order +
/// shape, each narrowing filter, the four <c>enrollmentState</c> values (incl. derived past-expiry and the
/// validityDays==0 no-expiry case), the <c>prerequisiteSatisfied</c> predicate (true/false/vacuous, mirroring the
/// redeem gate), per-caller scoping (no IDOR), cross-tenant isolation, and 401/403/200 role gating.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CatalogueApiTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid SubjectId, Guid SpecId, StudentEntity Student)> SetupAsync()
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, subjectId, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        return (tenant, gradeId, subjectId, specId, student);
    }

    private async Task<List<CatalogueSessionResponse>> GetCatalogueAsync(HttpClient client, string query = "")
        => (await client.GetFromJsonAsync<List<CatalogueSessionResponse>>(
            $"/api/me/catalogue{query}", TestJson.Options))!;

    // ── Published-only + shape + ordering ────────────────────────────────────
    [Fact]
    public async Task Catalogue_returns_only_published_sessions_newest_first_with_the_card_shape()
    {
        var (tenant, gradeId, subjectId, specId, student) = await SetupAsync();

        var older = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 100m, validityDays: 90, videoCount: 2);
        var newer = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 250m, validityDays: 30, videoCount: 3);
        await factory.SeedSessionAsync(tenant, gradeId, specId, SessionStatus.Draft);
        await factory.SeedSessionAsync(tenant, gradeId, specId, SessionStatus.Archived);

        var result = await GetCatalogueAsync(factory.CreateClientForStudent(tenant, student.Id));

        // Only the two published sessions, newest (by CreatedAtUtc) first.
        result.Select(x => x.Id).Should().Equal(newer.Id, older.Id);

        var card = result.Single(x => x.Id == newer.Id);
        card.Title.Should().Be(newer.Title);
        card.Price.Should().Be(250m);
        card.ValidityDays.Should().Be(30);
        card.VideoCount.Should().Be(3);
        card.ThumbnailUrl.Should().BeNull();             // no thumbnail seeded
        card.GradeId.Should().Be(gradeId);
        card.GradeName.Should().NotBeNullOrWhiteSpace();
        card.SubjectId.Should().Be(subjectId);           // derived via the specialization
        card.SubjectName.Should().NotBeNullOrWhiteSpace();
        card.SpecializationId.Should().Be(specId);
        card.SpecializationName.Should().NotBeNullOrWhiteSpace();
        card.PrerequisiteSessionId.Should().BeNull();
        card.PrerequisiteSatisfied.Should().BeTrue();    // vacuous
        card.EnrollmentState.Should().Be("NotEnrolled");
        card.EnrolledExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Catalogue_is_empty_when_the_tenant_has_no_published_sessions()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        await factory.SeedSessionAsync(tenant, gradeId, specId, SessionStatus.Draft);

        var result = await GetCatalogueAsync(factory.CreateClientForStudent(tenant, student.Id));

        result.Should().BeEmpty();
    }

    // ── Filters narrow ───────────────────────────────────────────────────────
    [Fact]
    public async Task Each_filter_narrows_the_result()
    {
        var (tenant, gradeId, subjectId, specId, student) = await SetupAsync();

        // A second grade/subject/specialization so each filter can exclude something.
        var grade2 = await factory.SeedGradeAsync(tenant);
        var subject2 = await factory.SeedSubjectAsync(tenant);
        var spec2 = await factory.SeedSpecializationAsync(tenant, subject2.Id);

        var a = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);        // grade1/subject1/spec1
        var b = await factory.SeedSessionWithContentAsync(tenant, grade2.Id, spec2.Id);    // grade2/subject2/spec2
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        (await GetCatalogueAsync(studentClient)).Select(x => x.Id).Should().BeEquivalentTo([a.Id, b.Id]);

        (await GetCatalogueAsync(studentClient, $"?gradeId={gradeId}"))
            .Select(x => x.Id).Should().Equal(a.Id);
        (await GetCatalogueAsync(studentClient, $"?subjectId={subjectId}"))
            .Select(x => x.Id).Should().Equal(a.Id);
        (await GetCatalogueAsync(studentClient, $"?specializationId={spec2.Id}"))
            .Select(x => x.Id).Should().Equal(b.Id);
        (await GetCatalogueAsync(studentClient, $"?search={Uri.EscapeDataString(b.Title)}"))
            .Select(x => x.Id).Should().Equal(b.Id);
    }

    // ── enrollmentState (FR-STU-CAT-004, §C.1) ───────────────────────────────
    [Fact]
    public async Task EnrollmentState_is_Enrolled_with_expiry_for_an_active_enrollment()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 100m, validityDays: 90);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(await teacher.GenerateOneSerialAsync(session.Id));

        var card = (await GetCatalogueAsync(studentClient)).Single(x => x.Id == session.Id);

        card.EnrollmentState.Should().Be("Enrolled");
        card.EnrolledExpiresAtUtc.Should().NotBeNull();   // validity 90 ⇒ has an expiry
    }

    [Fact]
    public async Task EnrollmentState_is_Enrolled_with_null_expiry_when_validityDays_is_zero()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 100m, validityDays: 0);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(await teacher.GenerateOneSerialAsync(session.Id));

        var card = (await GetCatalogueAsync(studentClient)).Single(x => x.Id == session.Id);

        card.EnrollmentState.Should().Be("Enrolled");
        card.EnrolledExpiresAtUtc.Should().BeNull();      // no-expiry session
    }

    [Fact]
    public async Task EnrollmentState_derives_Expired_when_an_active_enrollment_is_past_its_expiry()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 100m, validityDays: 90);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        await studentClient.RedeemAsync(await teacher.GenerateOneSerialAsync(session.Id));

        // Back-date the expiry into the past — the row stays Active; the projection derives Expired (§C.1).
        await ExpireEnrollmentAsync(session.Id, student.Id);

        var card = (await GetCatalogueAsync(studentClient)).Single(x => x.Id == session.Id);

        card.EnrollmentState.Should().Be("Expired");
        card.EnrolledExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task EnrollmentState_is_Refunded_after_a_refund()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var enrollment = await studentClient.RedeemAsync(await teacher.GenerateOneSerialAsync(session.Id));

        await teacher.PostAsJsonAsync(
            $"/api/enrollments/{enrollment.Id}/refund", new RefundRequestBody("changed mind"), TestJson.Options);

        var card = (await GetCatalogueAsync(studentClient)).Single(x => x.Id == session.Id);

        card.EnrollmentState.Should().Be("Refunded");
        card.EnrolledExpiresAtUtc.Should().BeNull();
    }

    // ── prerequisiteSatisfied (FR-STU-CAT-002, §C.2) ─────────────────────────
    [Fact]
    public async Task PrerequisiteSatisfied_is_true_vacuously_when_the_prerequisite_has_no_questions()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var prereq = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);   // no question bank
        var dependent = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId);
        await factory.SetSessionPrerequisiteAsync(dependent.Id, prereq.Id);

        var card = (await GetCatalogueAsync(factory.CreateClientForStudent(tenant, student.Id)))
            .Single(x => x.Id == dependent.Id);

        card.PrerequisiteSessionId.Should().Be(prereq.Id);
        card.PrerequisiteTitle.Should().Be(prereq.Title);
        card.PrerequisiteSatisfied.Should().BeTrue();      // nothing to complete ⇒ vacuous pass
    }

    [Fact]
    public async Task PrerequisiteSatisfied_is_false_until_the_prerequisite_assignment_is_completed()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var prereq = await factory.SeedSessionWithQuestionsAsync(tenant, gradeId, specId, questionCount: 1, price: 100m);
        var dependent = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        await factory.SetSessionPrerequisiteAsync(dependent.Id, prereq.Id);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        // Prerequisite has a question bank but is not completed ⇒ the gate (and the badge) is unsatisfied.
        var before = (await GetCatalogueAsync(studentClient)).Single(x => x.Id == dependent.Id);
        before.PrerequisiteTitle.Should().Be(prereq.Title);
        before.PrerequisiteSatisfied.Should().BeFalse();

        // Enroll in + complete the prerequisite assignment ⇒ the flag flips true (same predicate the gate uses).
        await studentClient.RedeemAsync(await teacher.GenerateOneSerialAsync(prereq.Id));
        await studentClient.CompleteAssignmentCorrectlyAsync(prereq.Id);

        var after = (await GetCatalogueAsync(studentClient)).Single(x => x.Id == dependent.Id);
        after.PrerequisiteSatisfied.Should().BeTrue();
    }

    // ── Per-caller scoping (no IDOR) ─────────────────────────────────────────
    [Fact]
    public async Task EnrollmentState_is_scoped_to_the_calling_student()
    {
        var (tenant, gradeId, _, specId, student) = await SetupAsync();
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var other = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // Only `student` redeems.
        await factory.CreateClientForStudent(tenant, student.Id)
            .RedeemAsync(await teacher.GenerateOneSerialAsync(session.Id));

        (await GetCatalogueAsync(factory.CreateClientForStudent(tenant, student.Id)))
            .Single(x => x.Id == session.Id).EnrollmentState.Should().Be("Enrolled");

        // The other student in the same tenant sees the same card as NotEnrolled.
        (await GetCatalogueAsync(factory.CreateClientForStudent(tenant, other.Id)))
            .Single(x => x.Id == session.Id).EnrollmentState.Should().Be("NotEnrolled");
    }

    // ── Cross-tenant isolation (NFR-SEC-010) ─────────────────────────────────
    [Fact]
    public async Task Catalogue_is_isolated_to_the_callers_tenant()
    {
        var (tenantA, gradeA, _, specA, _) = await SetupAsync();
        var sessionA = await factory.SeedSessionWithContentAsync(tenantA, gradeA, specA);

        var (tenantB, gradeB, _, specB, studentB) = await SetupAsync();
        var sessionB = await factory.SeedSessionWithContentAsync(tenantB, gradeB, specB);

        var result = await GetCatalogueAsync(factory.CreateClientForStudent(tenantB, studentB.Id));

        result.Select(x => x.Id).Should().Contain(sessionB.Id).And.NotContain(sessionA.Id);
    }

    // ── Role gating (§A.3) ───────────────────────────────────────────────────
    [Fact]
    public async Task Catalogue_is_student_only()
    {
        var (tenant, _, _, _, student) = await SetupAsync();

        // Anonymous → 401.
        (await factory.CreateClient().GetAsync("/api/me/catalogue"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Staff token → 403.
        (await factory.CreateClientFor(StaffRole.Teacher, tenant).GetAsync("/api/me/catalogue"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Student token → 200.
        (await factory.CreateClientForStudent(tenant, student.Id).GetAsync("/api/me/catalogue"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Back-dates an enrollment's expiry into the past without touching its status (the writer never
    /// flips Status to Expired) — so the projection's derived-Expired path (§C.1) can be exercised.</summary>
    private async Task ExpireEnrollmentAsync(Guid sessionId, Guid studentId)
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        await factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters()
            .Where(e => e.StudentId == studentId && e.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAtUtc, past)));
    }
}
