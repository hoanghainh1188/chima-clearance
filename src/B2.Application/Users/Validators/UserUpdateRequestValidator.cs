using B2.Shared.Contracts.Users;
using FluentValidation;

namespace B2.Application.Users.Validators;

public sealed class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequest>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(200).When(x => x.FullName is not null);
    }
}
