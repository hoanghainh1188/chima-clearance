namespace B2.Application.DailyHeaders;

public sealed record UpdateOfficerNameCommand(DateOnly Date, string Name, Guid? ActorUserId = null);
