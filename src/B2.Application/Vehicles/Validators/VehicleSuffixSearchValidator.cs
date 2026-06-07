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
        // Empty suffix is allowed and means "return all (unexited) vehicles".
        // A non-empty suffix must be the last 3-4 plate digits.
        RuleFor(x => x.Suffix)
            .Matches("^([0-9]{3,4})?$");
    }
}
