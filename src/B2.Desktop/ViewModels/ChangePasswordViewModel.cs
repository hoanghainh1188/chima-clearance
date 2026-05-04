using System.Net;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly IB2Api _api;
    private readonly AuthTokenHandler _auth;
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _infoMessage;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// True when the page was opened from the post-login user menu (modal mode).
    /// In that case the user can dismiss without changing the password. The
    /// pre-login force-rotate flow leaves this <c>false</c> so the prompt
    /// cannot be skipped.
    /// </summary>
    [ObservableProperty]
    private bool _canCancel;

    public ChangePasswordViewModel(IB2Api api, AuthTokenHandler auth, IAppNavigator navigator)
    {
        _api = api;
        _auth = auth;
        _navigator = navigator;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        InfoMessage = null;

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = AppResources.ChangePassword_Error_Mismatch;
            return;
        }

        IsBusy = true;
        try
        {
            await _api.ChangePasswordAsync(new ChangePasswordRequest(CurrentPassword, NewPassword));
            _auth.AccessToken = null;
            InfoMessage = AppResources.ChangePassword_SuccessRelogin;
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            await _navigator.NavigateToLoginAsync().ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = ex.Content?.Contains("INVALID_CREDENTIALS", StringComparison.Ordinal) == true
                ? AppResources.ChangePassword_Error_WrongCurrent
                : AppResources.ChangePassword_Error_Weak;
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.ChangePassword_Error_Generic;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.ChangePassword_Error_Generic;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!CanCancel)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page is not null)
        {
            await page.Navigation.PopModalAsync().ConfigureAwait(false);
        }
    }
}
