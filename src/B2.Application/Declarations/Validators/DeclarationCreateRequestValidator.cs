using System.Text.RegularExpressions;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class DeclarationCreateRequestValidator : AbstractValidator<DeclarationCreateRequest>
{
    /// <summary>A parent row with more numbers than this is a data-entry mistake, not a batch.</summary>
    public const int MaxNumbersPerDeclaration = 20;

    private static readonly Regex DeclNumber = new("^[0-9]{12}$", RegexOptions.Compiled);

    public DeclarationCreateRequestValidator()
    {
        RuleFor(x => x.DeclarationNumbers)
            .NotEmpty()
            .Must(list => list is null || list.Count <= MaxNumbersPerDeclaration)
            .Must(list => list is null || list.All(n => DeclNumber.IsMatch(n ?? string.Empty)))
            .Must(list => list is null || list.Distinct(StringComparer.Ordinal).Count() == list.Count)
            .WithMessage("Mỗi số tờ khai phải gồm đúng 12 chữ số và không được lặp lại trên cùng một dòng.");

        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GoodsName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FirstVehicle.PlateNumber).Must(PlateNumber.IsValid);
        RuleFor(x => x.FirstVehicle.Note).MaximumLength(500).When(x => x.FirstVehicle.Note is not null);
    }
}
