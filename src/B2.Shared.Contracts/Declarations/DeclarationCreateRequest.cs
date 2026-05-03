namespace B2.Shared.Contracts.Declarations;

public sealed record DeclarationCreateRequest(
    string DeclarationNumber,
    Guid ProcessorId,
    string CompanyName,
    string GoodsName,
    Guid YardId,
    VehicleAddRequest FirstVehicle);
