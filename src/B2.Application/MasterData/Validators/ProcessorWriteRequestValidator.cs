using B2.Shared.Contracts.MasterData;
using FluentValidation;

namespace B2.Application.MasterData.Validators;

public sealed class ProcessorWriteRequestValidator : AbstractValidator<ProcessorWriteRequest>
{
    public ProcessorWriteRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}
