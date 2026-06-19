using FluentValidation;

namespace SalahBahazad.Application.Features.Questions.Queries.ListQuestions;

internal sealed class ListQuestionsValidator : AbstractValidator<ListQuestionsQuery>
{
    public ListQuestionsValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
