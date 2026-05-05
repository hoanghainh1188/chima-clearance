using FluentValidation;

namespace B2.Application.DailyHeaders;

public sealed class RecomputeDailySummaryCommandValidator : AbstractValidator<RecomputeDailySummaryCommand>
{
    public RecomputeDailySummaryCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
