using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class VehicleAddRequestValidator : AbstractValidator<VehicleAddRequest>
{
    private static readonly System.Text.RegularExpressions.Regex Plate =
        new("^[0-9]{2}[A-Za-z]{1,2}[0-9]{4,5}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public VehicleAddRequestValidator()
    {
        RuleFor(x => x.PlateNumber).NotEmpty().Must(p => Plate.IsMatch(p ?? string.Empty));
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
        RuleFor(x => x.TrailerOrContainerNumber)
            .MaximumLength(20)
            .When(x => x.TrailerOrContainerNumber is not null);
    }
}
