namespace B2.Application.Common;

/// <summary>
/// Central home for FluentValidation failure messages raised in the Application
/// layer. These are API/diagnostic strings, not user-facing copy: B2.Desktop maps
/// validation failures to its own vi.resx text by status code rather than echoing
/// the server message, so they stay here (the Application layer cannot reference
/// B2.Desktop's resources without breaking the Constitution V dependency rule).
/// </summary>
internal static class ValidationMessages
{
    public const string OfficerNameEditWindow = "Officer name can only be edited for today's or yesterday's header.";
}
