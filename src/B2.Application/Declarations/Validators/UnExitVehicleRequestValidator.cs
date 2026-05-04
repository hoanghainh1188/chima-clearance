using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class UnExitVehicleRequestValidator : AbstractValidator<UnExitVehicleRequest>
{
    public UnExitVehicleRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
