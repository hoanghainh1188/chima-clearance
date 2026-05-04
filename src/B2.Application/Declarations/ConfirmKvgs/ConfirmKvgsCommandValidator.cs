using FluentValidation;

namespace B2.Application.Declarations.ConfirmKvgs;

public sealed class ConfirmKvgsCommandValidator : AbstractValidator<ConfirmKvgsCommand>
{
    public ConfirmKvgsCommandValidator()
    {
        RuleFor(x => x.DeclarationId).NotEmpty();
    }
}
