using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Shared.Contracts.Users;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2.Desktop.ViewModels.Admin;

public partial class UsersViewModel : ObservableObject
{
    private readonly Services.Api.IB2Api _api;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private IReadOnlyList<UserDto> _users = Array.Empty<UserDto>();

    /// <summary>Wireframe § 8 error banner correlationId binding.</summary>
    public ISessionCorrelationService Correlation { get; }

    public UsersViewModel(Services.Api.IB2Api api, ISessionCorrelationService correlation)
    {
        _api = api;
        Correlation = correlation;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _api.ListUsersAsync(1, 100);
            Users = list.Items;
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Users_Error_Load;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
