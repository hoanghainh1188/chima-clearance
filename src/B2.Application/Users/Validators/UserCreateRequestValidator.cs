using B2.Application.Common;
using B2.Shared.Contracts.Users;
using FluentValidation;

namespace B2.Application.Users.Validators;

public sealed class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TemporaryPassword).NotEmpty().Must(PasswordRules.MeetsComplexity)
            .WithMessage("Temporary password must meet complexity rules.");
    }
}
