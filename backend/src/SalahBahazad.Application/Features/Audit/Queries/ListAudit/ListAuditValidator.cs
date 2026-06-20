using FluentValidation;

namespace SalahBahazad.Application.Features.Audit.Queries.ListAudit;

internal sealed class ListAuditValidator : AbstractValidator<ListAuditQuery>
{
    public ListAuditValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        // Feed page (contract #1): pageSize ≤ 100.
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        // When an explicit range is given, it must be ordered.
        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'From' must be on or before 'To'.");
    }
}
