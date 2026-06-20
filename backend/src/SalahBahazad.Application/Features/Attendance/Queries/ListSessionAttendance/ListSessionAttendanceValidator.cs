using FluentValidation;

namespace SalahBahazad.Application.Features.Attendance.Queries.ListSessionAttendance;

internal sealed class ListSessionAttendanceValidator : AbstractValidator<ListSessionAttendanceQuery>
{
    public ListSessionAttendanceValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
