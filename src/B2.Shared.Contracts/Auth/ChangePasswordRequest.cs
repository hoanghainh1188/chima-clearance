namespace B2.Shared.Contracts.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
