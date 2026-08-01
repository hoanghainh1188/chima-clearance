namespace B2.Server.Api.Auth;

public static class B2Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string GateOperator = nameof(GateOperator);

    /// <summary>
    /// Anyone who works a gate lane: the exit-gate columns (KVGS tick, ghi chú) are the same
    /// officer's job in business rules § B.2, so they are not split across roles.
    /// </summary>
    public const string GateStaff = nameof(GateStaff);

    public const string SupervisorOrAbove = nameof(SupervisorOrAbove);
    public const string MasterRead = nameof(MasterRead);
    public const string AnyAuthenticated = nameof(AnyAuthenticated);
}
