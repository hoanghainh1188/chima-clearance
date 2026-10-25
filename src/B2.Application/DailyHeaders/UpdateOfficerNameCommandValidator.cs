using B2.Application.Abstractions;
using B2.Application.Common;
using FluentValidation;

namespace B2.Application.DailyHeaders;

public sealed class UpdateOfficerNameCommandValidator : AbstractValidator<UpdateOfficerNameCommand>
{
    public UpdateOfficerNameCommandValidator(IClock clock)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        // INV-11: today's header, or yesterday's — a night shift ends after midnight and the
        // officer signs the header of the shift that just closed (#186). Older headers stay locked.
        RuleFor(x => x.Date)
            .Must(date => IsWithinEditWindow(date, VietnamTime.BusinessDateFromUtc(clock.UtcNow)))
            .WithMessage(ValidationMessages.OfficerNameEditWindow);
    }

    private static bool IsWithinEditWindow(DateOnly date, DateOnly today) =>
        date == today || date == today.AddDays(-1);
}
