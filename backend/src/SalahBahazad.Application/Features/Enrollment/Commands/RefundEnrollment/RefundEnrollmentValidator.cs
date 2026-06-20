using FluentValidation;

namespace SalahBahazad.Application.Features.Enrollment.Commands.RefundEnrollment;

internal sealed class RefundEnrollmentValidator : AbstractValidator<RefundEnrollmentCommand>
{
    public RefundEnrollmentValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
