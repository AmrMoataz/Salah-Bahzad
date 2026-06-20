using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Queries.ListSessions;

internal sealed class ListSessionsValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        // Reused as a filter dropdown by the codes screen (large page to list all sessions); the contract
        // sets no pageSize ceiling.
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
