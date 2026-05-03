using B2.Application.Common;
using B2.Shared.Contracts.Auth;
using FluentValidation;

namespace B2.Application.Auth.Validators;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().Must(PasswordRules.MeetsComplexity)
            .WithMessage("Password must be at least 10 characters and meet complexity rules.");
    }
}
