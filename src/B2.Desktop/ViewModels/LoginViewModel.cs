using System.Net;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IB2Api _api;
    private readonly AuthTokenHandler _auth;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(IB2Api api, AuthTokenHandler auth)
    {
        _api = api;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var response = await _api.LoginAsync(new LoginRequest(Username.Trim(), Password));
            _auth.AccessToken = response.AccessToken;

            if (response.MustChangePassword)
            {
                await Shell.Current.GoToAsync("auth/change-password");
                return;
            }

            if (response.Role == UserRoleDto.Admin)
            {
                await Shell.Current.GoToAsync("admin/users");
                return;
            }

            ErrorMessage = AppResources.Login_Error_Generic;
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = AppResources.Login_Error_Generic;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Locked)
        {
            ErrorMessage = AppResources.Login_AccountLocked;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
