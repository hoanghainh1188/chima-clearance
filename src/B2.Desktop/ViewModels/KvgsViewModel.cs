using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public sealed partial class KvgsViewModel : ObservableObject, IDisposable
{
    private static readonly CompositeFormat KvgsToastFormat = CompositeFormat.Parse(AppResources.Kvgs_Toast_Confirmed_Format);
    private static readonly CompositeFormat KvgsConfirmMessageFormat = CompositeFormat.Parse(AppResources.Kvgs_ConfirmMessage_Format);
    private static readonly CompositeFormat KvgsErrorConfirmFormat = CompositeFormat.Parse(AppResources.Kvgs_Error_Confirm_Format);
    private static readonly CompositeFormat KvgsLedgerErrorLoadFormat = CompositeFormat.Parse(AppResources.Ledger_Error_Load_Format);

    private readonly IB2Api _api;
    private readonly ConnectivityService _connectivity;
    private readonly IModalService _modal;
    private readonly IToastService _toast;
    private bool _disposed;

    public KvgsViewModel(IB2Api api, ConnectivityService connectivity, IModalService modal, IToastService toast)
    {
        _api = api;
        _connectivity = connectivity;
        _modal = modal;
        _toast = toast;
        _connectivity.SuggestedListRefresh += OnSuggestedRefresh;
        _connectivity.DeclarationLedgerChanged += OnLedgerPush;
        _connectivity.ModeChanged += OnConnectivityChanged;
        Rows.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(NotifyViewStates);
        OnConnectivityChanged(this, EventArgs.Empty);
    }

    public ObservableCollection<LedgerRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _connectivityLabel = AppResources.Connectivity_Offline;

    [ObservableProperty]
    private bool _showOfflineBanner;

    [ObservableProperty]
    private bool _showPollingBanner;

    public bool ShowEmpty => !IsLoading && string.IsNullOrEmpty(ErrorMessage) && Rows.Count == 0;

    public bool ShowDataGrid => !IsLoading && string.IsNullOrEmpty(ErrorMessage) && Rows.Count > 0;

    private void NotifyViewStates()
    {
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowDataGrid));
    }

    private void OnConnectivityChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ShowOfflineBanner = _connectivity.Mode == ConnectivityMode.Offline;
            ShowPollingBanner = _connectivity.Mode == ConnectivityMode.PollingFallback;
            ConnectivityLabel = _connectivity.Mode switch
            {
                ConnectivityMode.Online => AppResources.Connectivity_Online,
                ConnectivityMode.PollingFallback => AppResources.Connectivity_Polling,
                _ => AppResources.Connectivity_Offline,
            };
        });
    }

    private void OnSuggestedRefresh()
    {
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync().ConfigureAwait(true));
    }

    private void OnLedgerPush(Guid declarationId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _ = declarationId;
            await LoadAsync().ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private Task Init() => LoadAsync();

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private async Task ConfirmAsync(LedgerRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var ok = await _modal
            .ConfirmAsync(
                AppResources.Kvgs_ConfirmTitle,
                string.Format(CultureInfo.CurrentCulture, KvgsConfirmMessageFormat, row.DeclarationMasked),
                AppResources.Common_Yes,
                AppResources.Common_No)
            .ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        try
        {
            await _api.ConfirmKvgsAsync(row.Dto.Id).ConfigureAwait(true);
            var toast = string.Format(CultureInfo.CurrentCulture, KvgsToastFormat, row.DeclarationMasked);
            await _toast.ShowAsync(toast).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (ApiException ex)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, KvgsErrorConfirmFormat, (int)ex.StatusCode);
            NotifyViewStates();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Ledger_Error_Network;
            NotifyViewStates();
        }
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        ErrorMessage = null;
        IsLoading = true;
        NotifyViewStates();
        try
        {
            var response = await _api.ListDeclarationsAsync(
                    1,
                    200,
                    "AllExited",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
                .ConfigureAwait(true);
            Rows.Clear();
            foreach (var dto in response.Items)
            {
                Rows.Add(LedgerRowViewModel.FromDto(dto, 0));
            }

            NotifyViewStates();
        }
        catch (ApiException ex)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, KvgsLedgerErrorLoadFormat, (int)ex.StatusCode);
            NotifyViewStates();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Ledger_Error_Network;
            NotifyViewStates();
        }
        finally
        {
            IsLoading = false;
            NotifyViewStates();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectivity.SuggestedListRefresh -= OnSuggestedRefresh;
        _connectivity.DeclarationLedgerChanged -= OnLedgerPush;
        _connectivity.ModeChanged -= OnConnectivityChanged;
    }
}
