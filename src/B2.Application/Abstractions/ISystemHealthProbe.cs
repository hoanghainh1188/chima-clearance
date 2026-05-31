using B2.Application.Admin.Health;

namespace B2.Application.Abstractions;

/// <summary>Gathers process + storage + data health metrics for the F-18 dashboard.</summary>
public interface ISystemHealthProbe
{
    Task<SystemHealthSnapshot> ReadAsync(CancellationToken cancellationToken);
}
