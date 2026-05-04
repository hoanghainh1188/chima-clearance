namespace B2.Shared.Contracts.Declarations;

public sealed record DeclarationDetailResponse(
    DeclarationDto Declaration,
    IReadOnlyList<VehicleDetailDto> Vehicles);
