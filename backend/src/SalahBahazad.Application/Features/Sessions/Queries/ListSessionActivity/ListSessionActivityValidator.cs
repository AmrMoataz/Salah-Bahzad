using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessionActivity;

internal sealed class ListSessionActivityValidator : AbstractValidator<ListSessionActivityQuery>
{
    public ListSessionActivityValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
