using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudentActivity;

internal sealed class ListStudentActivityValidator : AbstractValidator<ListStudentActivityQuery>
{
    public ListStudentActivityValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
