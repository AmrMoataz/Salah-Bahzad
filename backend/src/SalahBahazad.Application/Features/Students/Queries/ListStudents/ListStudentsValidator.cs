using FluentValidation;

namespace SalahBahazad.Application.Features.Students.Queries.ListStudents;

internal sealed class ListStudentsValidator : AbstractValidator<ListStudentsQuery>
{
    public ListStudentsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        // Reused as the unlock-modal student picker (contract §1); the contract sets no pageSize ceiling.
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
