using System.Text.RegularExpressions;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class DeclarationCreateRequestValidator : AbstractValidator<DeclarationCreateRequest>
{
    private static readonly Regex DeclNumber = new("^[0-9]{12}$", RegexOptions.Compiled);
    private static readonly Regex Plate = new("^[0-9]{2}[A-Za-z]{1,2}[0-9]{4,5}$", RegexOptions.Compiled);

    public DeclarationCreateRequestValidator()
    {
        RuleFor(x => x.DeclarationNumber).Must(s => DeclNumber.IsMatch(s ?? string.Empty));
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GoodsName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FirstVehicle.PlateNumber).Must(p => Plate.IsMatch(p ?? string.Empty));
        RuleFor(x => x.FirstVehicle.Note).MaximumLength(500).When(x => x.FirstVehicle.Note is not null);
    }
}
