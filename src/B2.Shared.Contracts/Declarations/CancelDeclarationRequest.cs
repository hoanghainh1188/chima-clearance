namespace B2.Shared.Contracts.Declarations;

/// <summary>FR-022 — Admin cancels a declaration; the reason is mandatory.</summary>
public sealed record CancelDeclarationRequest(string Reason);
