namespace B2.Application.Declarations.Cancel;

public sealed record CancelDeclarationCommand(Guid DeclarationId, string Reason);
