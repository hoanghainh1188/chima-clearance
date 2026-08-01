using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class VehicleAddRequestValidator : AbstractValidator<VehicleAddRequest>
{
    public VehicleAddRequestValidator()
    {
        // PlateNumber.IsValid normalises first (cột E: upper-case, separators stripped), so the
        // validator and the domain agree on what is acceptable instead of keeping rival regexes.
        RuleFor(x => x.PlateNumber).NotEmpty().Must(PlateNumber.IsValid);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
        RuleFor(x => x.TrailerOrContainerNumber)
            .MaximumLength(20)
            .When(x => x.TrailerOrContainerNumber is not null);
    }
}
