using B2.Application.Declarations.Note;
using FluentValidation;

namespace B2.Application.Declarations.Validators;

public sealed class UpdateVehicleNoteCommandValidator : AbstractValidator<UpdateVehicleNoteCommand>
{
    public UpdateVehicleNoteCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();

        // Same ceiling as the note captured at entry (VehicleAddRequestValidator).
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
    }
}
