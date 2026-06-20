using FluentValidation;

namespace SalahBahazad.Application.Features.Codes.Queries.ListCodes;

internal sealed class ListCodesValidator : AbstractValidator<ListCodesQuery>
{
    public ListCodesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        // The register loads larger pages for client-side bulk-select / export-selection over a whole batch
        // (contract §2); a batch is up to CodeBatch.MaxQuantity. The frozen contract sets no pageSize ceiling.
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
