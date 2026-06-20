using FluentValidation;

namespace SalahBahazad.Application.Features.Enrollment.Queries.ListStudentEnrollments;

internal sealed class ListStudentEnrollmentsValidator : AbstractValidator<ListStudentEnrollmentsQuery>
{
    public ListStudentEnrollmentsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
