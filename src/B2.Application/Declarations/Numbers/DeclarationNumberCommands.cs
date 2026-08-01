namespace B2.Application.Declarations.Numbers;

/// <summary>Cột C is editable at the entry gate — a batch can gain another tờ khai later.</summary>
public sealed record AddDeclarationNumberCommand(Guid DeclarationId, string Number);

public sealed record RemoveDeclarationNumberCommand(Guid DeclarationId, string Number);
