using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudentLoginHistory;

internal sealed class ListStudentLoginHistoryValidator : AbstractValidator<ListStudentLoginHistoryQuery>
{
    public ListStudentLoginHistoryValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
