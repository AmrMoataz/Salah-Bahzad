using FluentValidation;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Codes.Commands.GenerateCodeBatch;

internal sealed class GenerateCodeBatchValidator : AbstractValidator<GenerateCodeBatchCommand>
{
    public GenerateCodeBatchValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(CodeBatch.MinQuantity, CodeBatch.MaxQuantity);
        RuleFor(x => x.Value!.Value).GreaterThanOrEqualTo(0).When(x => x.Value.HasValue);
    }
}
