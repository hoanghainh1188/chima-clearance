using B2.Application.Common;
using B2.Shared.Contracts.Users;
using FluentValidation;

namespace B2.Application.Users.Validators;

public sealed class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    public PasswordResetRequestValidator()
    {
        RuleFor(x => x.TemporaryPassword).NotEmpty().Must(PasswordRules.MeetsComplexity);
    }
}
