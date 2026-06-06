using System.Net;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IB2Api _api;
    private readonly AuthTokenHandler _auth;
    private readonly IMainShellFactory _mainShellFactory;
    private readonly ISessionContext _session;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(
        IB2Api api,
        AuthTokenHandler auth,
        IMainShellFactory mainShellFactory,
        ISessionContext session)
    {
        _api = api;
        _auth = auth;
        _mainShellFactory = mainShellFactory;
        _session = session;
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
                Application.Current!.MainPage = _mainShellFactory.CreateForcedPasswordChange();
                return;
            }

            _session.Set(response);
            Application.Current!.MainPage = _mainShellFactory.CreateMainShell();
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = AppResources.Login_Error_Generic;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Locked)
        {
            ErrorMessage = AppResources.Login_AccountLocked;
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.Login_Error_Generic;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Login_Error_Generic;
        }
        catch (Exception)
        {
            // Defensive: any unexpected failure (e.g. navigation / page
            // construction) surfaces the generic login error rather than
            // leaving the UI in a blank / hung state.
            ErrorMessage = AppResources.Login_Error_Generic;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
