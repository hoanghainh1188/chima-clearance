using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Desktop.Services.Users;
using B2.Shared.Contracts.Users;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels.Admin;

/// <summary>
/// F-02 Admin Users screen (wireframe § 6.8): list + search/filter, create, edit, reset password,
/// deactivate / reactivate. No delete — F-02 only deactivates. Every write refuses while the
/// server is unreachable (FR-028 🔴) and maps the server's 409 codes to specific Vietnamese copy.
/// </summary>
public sealed partial class UsersViewModel : ObservableObject, IDisposable
{
    private static readonly CompositeFormat SummaryFormat = CompositeFormat.Parse(AppResources.Users_Summary_Format);
    private static readonly CompositeFormat LastUpdatedFormat =
        CompositeFormat.Parse(AppResources.Users_LastUpdated_Format);
    private static readonly CompositeFormat UpdatedToastFormat =
        CompositeFormat.Parse(AppResources.Users_Toast_Updated_Format);
    private static readonly CompositeFormat DeactivatedToastFormat =
        CompositeFormat.Parse(AppResources.Users_Toast_Deactivated_Format);
    private static readonly CompositeFormat ReactivatedToastFormat =
        CompositeFormat.Parse(AppResources.Users_Toast_Reactivated_Format);
    private static readonly CompositeFormat DeactivateConfirmFormat =
        CompositeFormat.Parse(AppResources.Users_Deactivate_ConfirmMessage_Format);
    private static readonly CompositeFormat ResetConfirmFormat =
        CompositeFormat.Parse(AppResources.Users_ResetPassword_ConfirmMessage_Format);
    private static readonly CompositeFormat TemporaryPasswordFormat =
        CompositeFormat.Parse(AppResources.Users_TemporaryPassword_Message_Format);

    private readonly IB2Api _api;
    private readonly IModalService _modal;
    private readonly IToastService _toast;
    private readonly IUserFormDialog _form;
    private readonly ISessionContext _session;
    private readonly ConnectivityService _connectivity;
    private IReadOnlyList<UserDto> _all = Array.Empty<UserDto>();
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    private IReadOnlyList<UserRow> _rows = Array.Empty<UserRow>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedStatusIndex;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string? _lastUpdatedText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    private bool _showOfflineBanner;

    [ObservableProperty]
    private bool _showPollingBanner;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    private bool _isBusy;

    public UsersViewModel(
        IB2Api api,
        IModalService modal,
        IToastService toast,
        IUserFormDialog form,
        ISessionContext session,
        ConnectivityService connectivity,
        ISessionCorrelationService correlation)
    {
        _api = api;
        _modal = modal;
        _toast = toast;
        _form = form;
        _session = session;
        _connectivity = connectivity;
        Correlation = correlation;
        _connectivity.ModeChanged += OnConnectivityChanged;
        OnConnectivityChanged(this, EventArgs.Empty);
    }

    /// <summary>Wireframe § 8 error banner correlationId binding.</summary>
    public ISessionCorrelationService Correlation { get; }

    /// <summary>Wireframe § 6.8 "Lọc" options, index-aligned with <see cref="UserStatusFilter"/>.</summary>
    public IReadOnlyList<string> StatusFilterOptions { get; } =
    [
        AppResources.Users_Filter_All,
        AppResources.Users_Filter_Active,
        AppResources.Users_Filter_Inactive,
    ];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool ShowEmpty => !IsLoading && !HasError && Rows.Count == 0;

    public bool ShowList => !IsLoading && Rows.Count > 0;

    private bool CanWrite => !IsBusy && !ShowOfflineBanner;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedStatusIndexChanged(int value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _api.ListUsersAsync(1, 200).ConfigureAwait(true);
            _all = list.Items;
            ApplyFilter();
            var now = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            LastUpdatedText = string.Format(CultureInfo.CurrentCulture, LastUpdatedFormat, now);
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.Users_Error_Load;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Users_Error_Load;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task AddUserAsync()
    {
        var input = await _form.ShowAsync(null).ConfigureAwait(true);
        if (input?.TemporaryPassword is null)
        {
            return;
        }

        await RunWriteAsync(async () =>
        {
            var request = new UserCreateRequest(input.Username, input.FullName, input.Role, input.TemporaryPassword);
            var created = await _api.CreateUserAsync(request).ConfigureAwait(true);
            await ShowTemporaryPasswordAsync(
                AppResources.Users_Create_DoneTitle,
                created.Username,
                input.TemporaryPassword).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task EditUserAsync(UserRow? row)
    {
        if (row is null)
        {
            return;
        }

        var input = await _form.ShowAsync(row.User).ConfigureAwait(true);
        if (input is null)
        {
            return;
        }

        await RunWriteAsync(async () =>
        {
            await _api.UpdateUserAsync(row.User.Id, new UserUpdateRequest(input.FullName, input.Role, null))
                .ConfigureAwait(true);
            await ToastAsync(UpdatedToastFormat, row.Username).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task ResetPasswordAsync(UserRow? row)
    {
        if (row is null)
        {
            return;
        }

        var confirm = await _modal.ConfirmAsync(
            AppResources.Users_ResetPassword_ConfirmTitle,
            string.Format(CultureInfo.CurrentCulture, ResetConfirmFormat, row.Username),
            AppResources.Users_ResetPassword_ConfirmAccept,
            AppResources.Common_Cancel).ConfigureAwait(true);
        if (!confirm)
        {
            return;
        }

        var password = TemporaryPassword.Generate();
        await RunWriteAsync(async () =>
        {
            await _api.ResetUserPasswordAsync(row.User.Id, new PasswordResetRequest(password)).ConfigureAwait(true);
            await ShowTemporaryPasswordAsync(AppResources.Users_ResetPassword_DoneTitle, row.Username, password)
                .ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task ToggleActiveAsync(UserRow? row)
    {
        if (row is null)
        {
            return;
        }

        var activate = !row.IsActive;
        if (!activate)
        {
            var confirm = await _modal.ConfirmAsync(
                AppResources.Users_Deactivate_ConfirmTitle,
                string.Format(CultureInfo.CurrentCulture, DeactivateConfirmFormat, row.Username),
                AppResources.Users_Action_Deactivate,
                AppResources.Common_Cancel).ConfigureAwait(true);
            if (!confirm)
            {
                return;
            }
        }

        await RunWriteAsync(async () =>
        {
            await _api.UpdateUserAsync(row.User.Id, new UserUpdateRequest(null, null, activate)).ConfigureAwait(true);
            await ToastAsync(activate ? ReactivatedToastFormat : DeactivatedToastFormat, row.Username)
                .ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectivity.ModeChanged -= OnConnectivityChanged;
    }

    /// <summary>Wraps a write: busy flag, error mapping (error toasts never auto-dismiss), reload.</summary>
    private async Task RunWriteAsync(Func<Task> write)
    {
        IsBusy = true;
        try
        {
            await write().ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (ApiException ex)
        {
            await _toast.ShowAsync(MapError(ex), ToastSeverity.Error).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await _toast.ShowAsync(AppResources.Common_Error_ServerUnreachable, ToastSeverity.Error)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string MapError(ApiException ex) => ApiErrorCode.FromProblemJson(ex.Content) switch
    {
        ApiErrorCode.UsernameTaken => AppResources.Users_Error_UsernameTaken,
        ApiErrorCode.UsernameInvalid => AppResources.Users_Error_UsernameInvalid,
        ApiErrorCode.CannotDeactivateSelf => AppResources.Users_Error_CannotDeactivateSelf,
        ApiErrorCode.LastAdminLockout => AppResources.Users_Error_LastAdminLockout,
        ApiErrorCode.ValidationError => AppResources.Users_Error_Validation,
        _ => AppResources.Users_Error_Save,
    };

    private Task ToastAsync(CompositeFormat format, string username) =>
        _toast.ShowAsync(string.Format(CultureInfo.CurrentCulture, format, username), ToastSeverity.Success);

    private Task ShowTemporaryPasswordAsync(string title, string username, string password) =>
        _modal.ShowAsync(
            title,
            string.Format(CultureInfo.CurrentCulture, TemporaryPasswordFormat, username, password),
            AppResources.Common_Close);

    private void ApplyFilter()
    {
        var status = (UserStatusFilter)Math.Clamp(SelectedStatusIndex, 0, StatusFilterOptions.Count - 1);
        var selfId = _session.UserId;
        Rows = UserListFilter.Apply(_all, SearchText, status)
            .Select(u => new UserRow(u, UserRoleLabels.For(u.Role), u.Id == selfId))
            .ToList();
        SummaryText = string.Format(
            CultureInfo.CurrentCulture,
            SummaryFormat,
            _all.Count,
            _all.Count(u => u.IsActive));
    }

    private void OnConnectivityChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ShowOfflineBanner = _connectivity.Mode == ConnectivityMode.Offline;
            ShowPollingBanner = _connectivity.Mode == ConnectivityMode.PollingFallback;
        });
    }
}
