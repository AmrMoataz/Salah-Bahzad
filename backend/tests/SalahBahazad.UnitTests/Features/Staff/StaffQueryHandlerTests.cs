using FluentAssertions;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Features.Staff.Queries.GetStaffById;
using SalahBahazad.Application.Features.Staff.Queries.ListStaff;
using SalahBahazad.Domain.Enums;
using static SalahBahazad.UnitTests.Features.Staff.StaffTestHelpers;
using StaffEntity = SalahBahazad.Domain.Entities.Staff;

namespace SalahBahazad.UnitTests.Features.Staff;

public class StaffQueryHandlerTests
{
    [Fact]
    public async Task List_filters_by_role()
    {
        var db = DbWith(
            NewStaff(email: "t@x.com", role: StaffRole.Teacher),
            NewStaff(email: "a1@x.com", role: StaffRole.Assistant),
            NewStaff(email: "a2@x.com", role: StaffRole.Assistant));
        var handler = new ListStaffHandler(db);

        var result = await handler.Handle(new ListStaffQuery(Role: StaffRole.Assistant), CancellationToken.None);

        result.Total.Should().Be(2);
        result.Items.Should().OnlyContain(s => s.Role == StaffRole.Assistant);
    }

    [Fact]
    public async Task List_filters_by_active_state()
    {
        var inactive = NewStaff(email: "i@x.com");
        inactive.Deactivate();
        var db = DbWith(NewStaff(email: "a@x.com"), inactive);
        var handler = new ListStaffHandler(db);

        var result = await handler.Handle(new ListStaffQuery(IsActive: false), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Email.Should().Be("i@x.com");
    }

    [Fact]
    public async Task List_searches_name_case_insensitively()
    {
        var db = DbWith(
            StaffEntity.Create(Guid.NewGuid(), "fb1", "Mariam Adel", "mariam@x.com", StaffRole.Assistant),
            StaffEntity.Create(Guid.NewGuid(), "fb2", "Hossam Fathy", "hossam@x.com", StaffRole.Assistant));
        var handler = new ListStaffHandler(db);

        var result = await handler.Handle(new ListStaffQuery(Search: "MARIAM"), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.DisplayName.Should().Be("Mariam Adel");
    }

    [Fact]
    public async Task List_paginates_and_reports_total()
    {
        var staff = Enumerable.Range(0, 5)
            .Select(i => StaffEntity.Create(Guid.NewGuid(), "fb" + i, $"User {i}", $"u{i}@x.com", StaffRole.Assistant))
            .ToArray();
        var handler = new ListStaffHandler(DbWith(staff));

        var result = await handler.Handle(new ListStaffQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetById_returns_dto()
    {
        var staff = NewStaff(email: "x@x.com");
        var handler = new GetStaffByIdHandler(DbWith(staff));

        var result = await handler.Handle(new GetStaffByIdQuery(staff.Id), CancellationToken.None);

        result.Id.Should().Be(staff.Id);
        result.Email.Should().Be("x@x.com");
    }

    [Fact]
    public async Task GetById_throws_NotFound_when_missing()
    {
        var handler = new GetStaffByIdHandler(DbWith());

        var act = () => handler.Handle(new GetStaffByIdQuery(Guid.NewGuid()), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
