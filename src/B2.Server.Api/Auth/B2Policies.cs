namespace B2.Server.Api.Auth;

public static class B2Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string GateOperator = nameof(GateOperator);
    public const string SupervisorOrAbove = nameof(SupervisorOrAbove);
    public const string MasterRead = nameof(MasterRead);
    public const string AnyAuthenticated = nameof(AnyAuthenticated);
}
