namespace B2.Shared.Contracts.Declarations;

/// <summary>
/// Cột C may hold several 12-digit numbers on one parent row ("có nhiều dãy số thì mỗi dãy số sẽ
/// tách thành từng dòng riêng biệt trong cùng 1 ô"), so the request takes an ordered list. The
/// first entry is the primary number shown wherever a single value is needed.
/// </summary>
public sealed record DeclarationCreateRequest(
    IReadOnlyList<string> DeclarationNumbers,
    Guid ProcessorId,
    string CompanyName,
    string GoodsName,
    Guid YardId,
    VehicleAddRequest FirstVehicle);
