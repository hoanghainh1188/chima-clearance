using FluentValidation;

namespace B2.Application.Declarations.Cancel;

public sealed class CancelDeclarationCommandValidator : AbstractValidator<CancelDeclarationCommand>
{
    public CancelDeclarationCommandValidator()
    {
        RuleFor(x => x.DeclarationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
