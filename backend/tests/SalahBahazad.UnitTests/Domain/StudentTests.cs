using FluentAssertions;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;
using StudentEntity = SalahBahazad.Domain.Entities.Student;

namespace SalahBahazad.UnitTests.Domain;

public class StudentTests
{
    private static StudentEntity NewPending() => StudentEntity.Register(
        tenantId: Guid.NewGuid(),
        firebaseUid: "fb-uid",
        serial: "STU-TEST01",
        fullName: "  Mariam Adel  ",
        phoneNumber: "  01099999999 ",
        parentPhonePrimary: "  01000000000 ",
        parentPhoneSecondary: "  ",
        gradeId: Guid.NewGuid(),
        cityId: Guid.NewGuid(),
        regionId: Guid.NewGuid(),
        schoolName: "  Nile School ",
        termsVersion: "v1",
        termsAcceptedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public void Register_creates_pending_and_trims_fields()
    {
        var student = NewPending();

        student.Status.Should().Be(StudentStatus.Pending);
        student.FullName.Should().Be("Mariam Adel");
        student.PhoneNumber.Should().Be("01099999999");
        student.ParentPhonePrimary.Should().Be("01000000000");
        student.ParentPhoneSecondary.Should().BeNull(); // blank → null
        student.SchoolName.Should().Be("Nile School");
        student.TermsVersion.Should().Be("v1");
        student.TermsAcceptedAtUtc.Should().NotBeNull();
        student.IdImageObjectKey.Should().BeNull();
        student.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_throws_when_fullName_blank(string fullName)
    {
        var act = () => StudentEntity.Register(
            Guid.NewGuid(), "fb", "STU-TEST01", fullName, "0111", "0100", null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School", "v1", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_throws_when_grade_missing()
    {
        var act = () => StudentEntity.Register(
            Guid.NewGuid(), "fb", "STU-TEST01", "Name", "0111", "0100", null,
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "School", "v1", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_sets_the_provided_serial()
    {
        var student = StudentEntity.Register(
            Guid.NewGuid(), "fb", "STU-ABC123", "Name", "0111", "0100", null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School", "v1", DateTimeOffset.UtcNow);

        student.Serial.Should().Be("STU-ABC123");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_throws_when_serial_blank(string serial)
    {
        var act = () => StudentEntity.Register(
            Guid.NewGuid(), "fb", serial, "Name", "0111", "0100", null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School", "v1", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resubmit_keeps_the_original_serial()
    {
        var student = NewPending();          // serial "STU-TEST01"
        student.Reject("Blurry ID photo");

        student.Resubmit(
            fullName: "Mariam Adel Hassan",
            phoneNumber: "01088888888",
            parentPhonePrimary: "01000000001",
            parentPhoneSecondary: null,
            gradeId: Guid.NewGuid(),
            cityId: Guid.NewGuid(),
            regionId: Guid.NewGuid(),
            schoolName: "New School",
            termsVersion: "v2",
            termsAcceptedAtUtc: DateTimeOffset.UtcNow);

        // Minted once at Register, stable across a reject→resubmit cycle (FR-APP-VID-003).
        student.Serial.Should().Be("STU-TEST01");
    }

    [Fact]
    public void AttachIdImage_sets_key()
    {
        var student = NewPending();
        student.AttachIdImage("students/id-images/x.jpg");
        student.IdImageObjectKey.Should().Be("students/id-images/x.jpg");
    }

    [Fact]
    public void Approve_moves_pending_to_active_and_raises_event()
    {
        var student = NewPending();

        student.Approve();

        student.Status.Should().Be(StudentStatus.Active);
        student.DomainEvents.OfType<StudentApprovedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Approve_throws_when_not_pending()
    {
        var student = NewPending();
        student.Approve();

        var act = () => student.Approve();

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_requires_a_reason(string reason)
    {
        var student = NewPending();

        var act = () => student.Reject(reason);

        act.Should().Throw<ArgumentException>();
        student.Status.Should().Be(StudentStatus.Pending);
    }

    [Fact]
    public void Reject_stores_reason_and_raises_event_with_reason()
    {
        var student = NewPending();

        student.Reject("  Duplicate account  ");

        student.Status.Should().Be(StudentStatus.Rejected);
        student.RejectionReason.Should().Be("Duplicate account");
        student.DomainEvents.OfType<StudentRejectedEvent>().Single().Reason.Should().Be("Duplicate account");
    }

    [Fact]
    public void Reject_throws_when_not_pending()
    {
        var student = NewPending();
        student.Approve();

        var act = () => student.Reject("late");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resubmit_moves_rejected_back_to_pending_and_overwrites_details()
    {
        var student = NewPending();
        student.Reject("Blurry ID photo");
        var newGrade = Guid.NewGuid();

        student.Resubmit(
            fullName: "  Mariam Adel Hassan  ",
            phoneNumber: "  01088888888 ",
            parentPhonePrimary: "  01000000001 ",
            parentPhoneSecondary: "  ",
            gradeId: newGrade,
            cityId: Guid.NewGuid(),
            regionId: Guid.NewGuid(),
            schoolName: "  New School ",
            termsVersion: "v2",
            termsAcceptedAtUtc: DateTimeOffset.UtcNow);

        student.Status.Should().Be(StudentStatus.Pending);
        student.RejectionReason.Should().BeNull();
        student.FullName.Should().Be("Mariam Adel Hassan");
        student.ParentPhoneSecondary.Should().BeNull(); // blank → null
        student.GradeId.Should().Be(newGrade);
        student.SchoolName.Should().Be("New School");
        student.TermsVersion.Should().Be("v2");
    }

    [Fact]
    public void Resubmit_throws_when_not_rejected()
    {
        var student = NewPending(); // pending, never rejected

        var act = () => student.Resubmit(
            "Name", "0111", "0100", null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School", "v1", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Deactivate_and_reactivate_toggle_status_with_events()
    {
        var student = NewPending();
        student.Approve();

        student.Deactivate();
        student.Status.Should().Be(StudentStatus.Inactive);
        student.DomainEvents.OfType<StudentDeactivatedEvent>().Should().ContainSingle();

        student.Reactivate();
        student.Status.Should().Be(StudentStatus.Active);
        student.DomainEvents.OfType<StudentReactivatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Deactivate_throws_when_not_active()
    {
        var student = NewPending(); // pending, not active

        var act = () => student.Deactivate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reactivate_throws_when_not_inactive()
    {
        var student = NewPending();
        student.Approve(); // active, not inactive

        var act = () => student.Reactivate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateContactInfo_updates_grade_and_phones()
    {
        var student = NewPending();
        var newGrade = Guid.NewGuid();

        student.UpdateContactInfo(newGrade, " 01999999999 ", " 01111111111 ", " 01222222222 ");

        student.GradeId.Should().Be(newGrade);
        student.PhoneNumber.Should().Be("01999999999");
        student.ParentPhonePrimary.Should().Be("01111111111");
        student.ParentPhoneSecondary.Should().Be("01222222222");
    }

    [Fact]
    public void UpdateContactInfo_throws_when_primary_phone_blank()
    {
        var student = NewPending();

        var act = () => student.UpdateContactInfo(Guid.NewGuid(), "0111", "  ", null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordSignIn_stamps_last_seen()
    {
        var student = NewPending();
        var now = DateTimeOffset.UtcNow;

        student.RecordSignIn(now);

        student.LastSeenAtUtc.Should().Be(now);
    }

    [Fact]
    public void SoftDelete_sets_audit_attribution()
    {
        var student = NewPending();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        student.SoftDelete(actor, now);

        student.IsDeleted.Should().BeTrue();
        student.DeletedById.Should().Be(actor);
        student.DeletedAtUtc.Should().Be(now);
    }
}
