using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudents;

internal sealed class ListStudentsValidator : AbstractValidator<ListStudentsQuery>
{
    public ListStudentsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
