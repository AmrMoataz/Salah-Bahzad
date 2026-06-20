using FluentValidation;

namespace SalahBahazad.Application.Features.Enrollment.Queries.ListSessionEnrollments;

internal sealed class ListSessionEnrollmentsValidator : AbstractValidator<ListSessionEnrollmentsQuery>
{
    public ListSessionEnrollmentsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
