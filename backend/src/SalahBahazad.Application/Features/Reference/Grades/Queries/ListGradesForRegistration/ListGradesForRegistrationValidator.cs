using FluentValidation;

namespace SalahBahazad.Application.Features.Reference.Grades.Queries.ListGradesForRegistration;

internal sealed class ListGradesForRegistrationValidator : AbstractValidator<ListGradesForRegistrationQuery>
{
    public ListGradesForRegistrationValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
    }
}
