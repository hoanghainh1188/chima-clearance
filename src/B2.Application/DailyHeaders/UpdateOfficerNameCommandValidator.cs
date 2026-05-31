using B2.Application.Abstractions;
using B2.Application.Common;
using FluentValidation;

namespace B2.Application.DailyHeaders;

public sealed class UpdateOfficerNameCommandValidator : AbstractValidator<UpdateOfficerNameCommand>
{
    public UpdateOfficerNameCommandValidator(IClock clock)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Date)
            .Must(date => date == VietnamTime.BusinessDateFromUtc(clock.UtcNow))
            .WithMessage(ValidationMessages.OfficerNameTodayOnly);
    }
}
