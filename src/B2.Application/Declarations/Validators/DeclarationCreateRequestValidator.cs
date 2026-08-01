using System.Text.RegularExpressions;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class DeclarationCreateRequestValidator : AbstractValidator<DeclarationCreateRequest>
{
    private static readonly Regex DeclNumber = new("^[0-9]{12}$", RegexOptions.Compiled);

    public DeclarationCreateRequestValidator()
    {
        RuleFor(x => x.DeclarationNumber).Must(s => DeclNumber.IsMatch(s ?? string.Empty));
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GoodsName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FirstVehicle.PlateNumber).Must(PlateNumber.IsValid);
        RuleFor(x => x.FirstVehicle.Note).MaximumLength(500).When(x => x.FirstVehicle.Note is not null);
    }
}
