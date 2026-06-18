using FluentValidation;

namespace SalahBahazad.Application.Features.Staff.Queries.ListStaff;

internal sealed class ListStaffValidator : AbstractValidator<ListStaffQuery>
{
    public ListStaffValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
