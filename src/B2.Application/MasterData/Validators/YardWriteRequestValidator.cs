using System.Text.RegularExpressions;
using B2.Shared.Contracts.MasterData;
using FluentValidation;

namespace B2.Application.MasterData.Validators;

public sealed class YardWriteRequestValidator : AbstractValidator<YardWriteRequest>
{
    private static readonly Regex Code = new("^[A-Z0-9]{1,20}$", RegexOptions.Compiled);

    public YardWriteRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Must(c => Code.IsMatch(c.Trim().ToUpperInvariant()));
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
