namespace B2.Shared.Contracts.MasterData;

public sealed record YardWriteRequest(string Code, string Name, bool? IsActive);
