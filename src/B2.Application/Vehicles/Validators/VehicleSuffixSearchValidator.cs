using FluentValidation;

namespace B2.Application.Vehicles.Validators;

public sealed class VehicleSuffixSearchQuery
{
    public string Suffix { get; init; } = "";
    public bool Unexited { get; init; } = true;
}

public sealed class VehicleSuffixSearchQueryValidator : AbstractValidator<VehicleSuffixSearchQuery>
{
    public VehicleSuffixSearchQueryValidator()
    {
        RuleFor(x => x.Suffix)
            .NotEmpty()
            .Matches("^[0-9]{3,4}$");
    }
}
