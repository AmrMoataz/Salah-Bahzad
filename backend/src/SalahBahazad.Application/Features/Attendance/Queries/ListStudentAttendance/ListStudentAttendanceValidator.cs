using FluentValidation;

namespace SalahBahazad.Application.Features.Attendance.Queries.ListStudentAttendance;

internal sealed class ListStudentAttendanceValidator : AbstractValidator<ListStudentAttendanceQuery>
{
    public ListStudentAttendanceValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
