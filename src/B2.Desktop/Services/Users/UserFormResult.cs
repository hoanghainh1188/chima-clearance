using B2.Shared.Contracts.Users;

namespace B2.Desktop.Services.Users;

/// <summary>
/// What the Add/Edit user dialog (wireframe § 6.8) returns on submit. <see cref="TemporaryPassword"/>
/// is only set in create mode; <see cref="Username"/> is read-only in edit mode.
/// </summary>
public sealed record UserFormResult(string Username, string FullName, UserRoleDto Role, string? TemporaryPassword);
