using FluentValidation;

namespace B2.Application.Declarations.List;

public sealed class ListDeclarationsQueryValidator : AbstractValidator<ListDeclarationsQuery>
{
    public ListDeclarationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
