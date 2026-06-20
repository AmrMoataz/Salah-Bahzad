using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.IntegrationTests;

/// <summary>
/// Enrollment engine: redeem (#12), unlock (#9), refund (#10), the shared create-or-extend path, the
/// one-active-enrollment rule, role gating and the now-real enrolledCount (contract §3/§5,
/// FR-PLAT-ENR-001..008, FR-PLAT-PAY-001/002, FR-PLAT-ATT-001).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EnrollmentApiTests(SalahBahazadApiFactory factory)
{
    private async Task<(Guid Tenant, Guid GradeId, Guid SpecId, Domain.Entities.Student Student)> SetupAsync(
        StudentStatus studentStatus = StudentStatus.Active)
    {
        var tenant = await factory.SeedTenantAsync();
        var (gradeId, _, specId) = await factory.SeedTaxonomyAsync(tenant);
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var student = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, studentStatus);
        return (tenant, gradeId, specId, student);
    }

    [Fact]
    public async Task Redeem_enrolls_marks_code_used_provisions_counters_payment_attendance_and_fires_event()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 100m, validityDays: 90, videoCount: 2, accessPerVideo: 3);

        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id); // value defaults to 100

        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var response = await studentClient.PostAsJsonAsync(
            "/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollment = (await response.Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;
        enrollment.Status.Should().Be("Active");
        enrollment.Method.Should().Be("Code");
        enrollment.Amount.Should().Be(100m);
        enrollment.CodeSerial.Should().Be(serial);
        enrollment.StudentName.Should().Be(student.FullName);
        enrollment.SessionTitle.Should().Be(session.Title);
        enrollment.ExpiresAtUtc.Should().NotBeNull(); // validity 90 ⇒ has expiry

        // Code → Used, joined to the student.
        var codeRow = await factory.QueryDbAsync(db =>
            db.Codes.IgnoreQueryFilters().FirstAsync(c => c.Serial == serial));
        codeRow.Status.Should().Be(CodeStatus.Used);
        codeRow.RedeemedByStudentId.Should().Be(student.Id);

        // Counters provisioned (one per video), payment Completed, attendance shell created.
        var enr = await factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters()
            .Include(e => e.VideoAccesses)
            .Include(e => e.Payments)
            .FirstAsync(e => e.Id == enrollment.Id));
        enr.VideoAccesses.Should().HaveCount(2);
        enr.VideoAccesses.Should().OnlyContain(a => a.AccessRemaining == 3 && a.AccessAllowed == 3);
        enr.Payments.Should().ContainSingle(p =>
            p.Status == PaymentStatus.Completed && p.Method == PaymentMethod.CodeRedemption && p.Amount == 100m);

        var hasAttendance = await factory.QueryDbAsync(db =>
            db.Attendances.IgnoreQueryFilters().AnyAsync(a => a.StudentId == student.Id && a.SessionId == session.Id));
        hasAttendance.Should().BeTrue();

        // The EnrollmentCreated event handler (side-effect seam) ran.
        factory.EnrollmentSideEffects.GeneratedFor.Should().Contain(enrollment.Id);
    }

    [Fact]
    public async Task Redeem_a_used_code_is_409()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var (cityId, regionId) = await factory.GetSeedLocationAsync();
        var other = await factory.SeedStudentAsync(tenant, gradeId, cityId, regionId, StudentStatus.Active);
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);

        // First student redeems it.
        (await factory.CreateClientForStudent(tenant, student.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // A second student cannot redeem a now-used code.
        var second = await factory.CreateClientForStudent(tenant, other.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Redeem_with_value_not_matching_price_is_409()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        // Mint a code with an explicit value that does NOT match the session price.
        var serial = await teacher.GenerateOneSerialAsync(session.Id, value: 999m);

        var response = await factory.CreateClientForStudent(tenant, student.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Redeem_when_already_actively_enrolled_is_409()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);

        var serialA = await teacher.GenerateOneSerialAsync(session.Id);
        var serialB = await teacher.GenerateOneSerialAsync(session.Id);

        (await studentClient.PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serialA), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // A second active enrollment for the same session is blocked (FR-PLAT-ENR-006).
        var second = await studentClient.PostAsJsonAsync(
            "/api/enrollments/redeem", new RedeemRequestBody(serialB), TestJson.Options);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unlock_grants_access_bypassing_code_and_price()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(
            tenant, gradeId, specId, price: 250m, validityDays: 0, videoCount: 2);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        var response = await teacher.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/unlock", new UnlockRequestBody(student.Id), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollment = (await response.Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;
        enrollment.Method.Should().Be("Unlock");
        enrollment.Amount.Should().Be(0m);          // price bypassed
        enrollment.CodeSerial.Should().BeNull();
        enrollment.ExpiresAtUtc.Should().BeNull();  // validityDays == 0 ⇒ no expiry

        var enr = await factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters()
            .Include(e => e.VideoAccesses)
            .Include(e => e.Payments)
            .FirstAsync(e => e.Id == enrollment.Id));
        enr.VideoAccesses.Should().HaveCount(2);
        enr.Payments.Should().ContainSingle(p => p.Method == PaymentMethod.Unlock && p.Amount == 0m);

        // Already-active unlock is rejected.
        var again = await teacher.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/unlock", new UnlockRequestBody(student.Id), TestJson.Options);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Refund_flips_status_returns_the_code_and_writes_a_reversing_payment()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);

        var enrollment = (await (await factory.CreateClientForStudent(tenant, student.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options))
            .Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;

        var refund = await teacher.PostAsJsonAsync(
            $"/api/enrollments/{enrollment.Id}/refund", new RefundRequestBody("changed mind"), TestJson.Options);
        refund.StatusCode.Should().Be(HttpStatusCode.OK);
        (await refund.Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!.Status.Should().Be("Refunded");

        // Code returned to circulation (Used → Active), join cleared.
        var codeRow = await factory.QueryDbAsync(db =>
            db.Codes.IgnoreQueryFilters().FirstAsync(c => c.Serial == serial));
        codeRow.Status.Should().Be(CodeStatus.Active);
        codeRow.RedeemedByStudentId.Should().BeNull();

        // Reversing payment recorded alongside the original.
        var enr = await factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters().Include(e => e.Payments).FirstAsync(e => e.Id == enrollment.Id));
        enr.Payments.Should().HaveCount(2);
        enr.Payments.Should().ContainSingle(p => p.Status == PaymentStatus.Refunded);

        // Refunding again is rejected.
        var again = await teacher.PostAsJsonAsync(
            $"/api/enrollments/{enrollment.Id}/refund", new RefundRequestBody(null), TestJson.Options);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Re_enroll_after_refund_reuses_the_row_without_a_duplicate()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m, videoCount: 2);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        var serial = await teacher.GenerateOneSerialAsync(session.Id);

        var first = (await (await studentClient.PostAsJsonAsync(
            "/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options))
            .Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;

        await teacher.PostAsJsonAsync(
            $"/api/enrollments/{first.Id}/refund", new RefundRequestBody(null), TestJson.Options);

        // The returned code can be redeemed again — reusing the same enrollment row (FR-PLAT-ENR-004).
        var second = (await (await studentClient.PostAsJsonAsync(
            "/api/enrollments/redeem", new RedeemRequestBody(serial), TestJson.Options))
            .Content.ReadFromJsonAsync<EnrollmentResult>(TestJson.Options))!;

        second.Id.Should().Be(first.Id);            // same row reused
        second.Status.Should().Be("Active");

        var count = await factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters().CountAsync(e => e.StudentId == student.Id && e.SessionId == session.Id));
        count.Should().Be(1);                       // no duplicate row

        var enr = await factory.QueryDbAsync(db => db.Enrollments
            .IgnoreQueryFilters().Include(e => e.VideoAccesses).FirstAsync(e => e.Id == first.Id));
        enr.VideoAccesses.Should().HaveCount(2);    // counters reset in place, not duplicated
    }

    [Fact]
    public async Task A_used_code_cannot_be_disabled()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        var batch = await teacher.GenerateBatchAsync(session.Id, quantity: 1);
        var code = (await teacher.ListBatchCodesAsync(batch.BatchId)).Single();

        await factory.CreateClientForStudent(tenant, student.Id)
            .PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody(code.Serial), TestJson.Options);

        (await teacher.PostAsync($"/api/codes/{code.Id}/disable", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EnrolledCount_reflects_active_enrollments()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        await teacher.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/unlock", new UnlockRequestBody(student.Id), TestJson.Options);

        var detail = await teacher.GetFromJsonAsync<SessionDetailResponse>(
            $"/api/sessions/{session.Id}", TestJson.Options);
        detail!.EnrolledCount.Should().Be(1);
    }

    [Fact]
    public async Task Session_and_student_enrollment_lists_return_the_enrollment()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);

        await teacher.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/unlock", new UnlockRequestBody(student.Id), TestJson.Options);

        var sessionList = await teacher.GetFromJsonAsync<PagedEnrollmentResponse>(
            $"/api/sessions/{session.Id}/enrollments", TestJson.Options);
        sessionList!.Items.Should().ContainSingle(i => i.StudentId == student.Id && i.Method == "Unlock");

        var studentList = await teacher.GetFromJsonAsync<PagedStudentEnrollmentResponse>(
            $"/api/students/{student.Id}/enrollments", TestJson.Options);
        studentList!.Items.Should().ContainSingle(i => i.SessionId == session.Id && i.Status == "Active");
    }

    [Fact]
    public async Task Redeem_is_student_only_and_staff_routes_are_staff_only()
    {
        var (tenant, gradeId, specId, student) = await SetupAsync();
        var session = await factory.SeedSessionWithContentAsync(tenant, gradeId, specId, price: 100m);

        // A staff token cannot use the student redeem endpoint (#12).
        var teacher = factory.CreateClientFor(StaffRole.Teacher, tenant);
        (await teacher.PostAsJsonAsync("/api/enrollments/redeem", new RedeemRequestBody("SB-XXXXX-XXXXX"), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A student token cannot use a staff route.
        var studentClient = factory.CreateClientForStudent(tenant, student.Id);
        (await studentClient.GetAsync($"/api/sessions/{session.Id}/enrollments"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Anonymous redeem is unauthorized.
        (await factory.CreateClient().PostAsJsonAsync(
                "/api/enrollments/redeem", new RedeemRequestBody("SB-XXXXX-XXXXX"), TestJson.Options))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
