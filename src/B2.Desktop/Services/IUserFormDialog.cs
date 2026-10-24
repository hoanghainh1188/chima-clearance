using B2.Desktop.Services.Users;
using B2.Shared.Contracts.Users;

namespace B2.Desktop.Services;

/// <summary>F-02 Add/Edit user modal (wireframe § 6.8).</summary>
public interface IUserFormDialog
{
    /// <summary>
    /// Shows the dialog — create mode when <paramref name="existing"/> is <c>null</c>, edit mode
    /// otherwise. Returns the submitted values, or <c>null</c> when the Admin cancels.
    /// </summary>
    Task<UserFormResult?> ShowAsync(UserDto? existing, CancellationToken cancellationToken = default);
}
