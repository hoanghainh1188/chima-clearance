using B2.Desktop.Services.Popups;
using B2.Desktop.Services.Users;
using B2.Shared.Contracts.Users;
using CommunityToolkit.Maui.Views;

namespace B2.Desktop.Services;

/// <summary>
/// Shows <see cref="UserFormPopup"/>. Marshals to the UI thread for the same reason as
/// <see cref="ModalService"/>: ShowPopupAsync throws off the UI thread.
/// </summary>
public sealed class UserFormDialog : IUserFormDialog
{
    public Task<UserFormResult?> ShowAsync(UserDto? existing, CancellationToken cancellationToken = default) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page is null)
            {
                return null;
            }

            var popup = new UserFormPopup(existing);
            var result = await page.ShowPopupAsync(popup, token: cancellationToken).ConfigureAwait(true);
            return result as UserFormResult;
        });
}
