using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessions;

internal sealed class ListSessionsValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
