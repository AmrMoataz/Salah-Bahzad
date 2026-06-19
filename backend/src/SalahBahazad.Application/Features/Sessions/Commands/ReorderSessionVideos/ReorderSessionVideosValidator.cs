using FluentValidation;

namespace SalahBahazad.Application.Features.Sessions.Commands.ReorderSessionVideos;

internal sealed class ReorderSessionVideosValidator : AbstractValidator<ReorderSessionVideosCommand>
{
    public ReorderSessionVideosValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.OrderedVideoIds).NotEmpty();
    }
}
