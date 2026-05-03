using B2.Shared.Contracts.Auth;
using FluentValidation;

namespace B2.Application.Auth.Validators;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
