using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Declarations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public partial class ExitViewModel : ObservableObject, IDisposable
{
    private const int DebounceMs = 500;
    private const int TooManyThreshold = 5;

    private static readonly CompositeFormat ExitErrorNoMatchFormat = CompositeFormat.Parse(AppResources.Exit_Error_NoMatch_Format);
    private static readonly CompositeFormat ExitToastExitMarkedFormat = CompositeFormat.Parse(AppResources.Exit_Toast_ExitMarked_Format);

    private readonly IB2Api _api;
    private readonly IToastService _toast;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _warningMessage;

    [ObservableProperty]
    private string _suffix = string.Empty;

    [ObservableProperty]
    private VehicleSearchResultDto? _selectedResult;

    public ObservableCollection<VehicleSearchResultDto> Results { get; } = new();

    public ExitViewModel(IB2Api api, IToastService toast)
    {
        _api = api;
        _toast = toast;
    }

    partial void OnSuffixChanged(string value) => _ = DebouncedSearchAsync();

    private async Task DebouncedSearchAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await Task.Delay(DebounceMs, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        await MainThread
            .InvokeOnMainThreadAsync(async () => await SearchCommand.ExecuteAsync(null).ConfigureAwait(true))
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        ErrorMessage = null;
        WarningMessage = null;
        Results.Clear();
        SelectedResult = null;

        var q = Suffix.Trim();
        if (q.Length < 3)
        {
            return;
        }

        IsSearching = true;
        try
        {
            var list = await _api.SearchVehiclesAsync(q, unexited: true).ConfigureAwait(false);
            foreach (var r in list)
            {
                Results.Add(r);
            }

            if (list.Count == 0)
            {
                ErrorMessage = string.Format(CultureInfo.CurrentCulture, ExitErrorNoMatchFormat, q);
            }
            else if (list.Count > TooManyThreshold)
            {
                WarningMessage = AppResources.Exit_Warning_TooMany;
            }

            if (list.Count == 1)
            {
                SelectedResult = list[0];
            }
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Exit_Error_Search;
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task MarkExitAsync(VehicleSearchResultDto? row)
    {
        var target = row ?? SelectedResult;
        if (target is null)
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            var key = Guid.NewGuid().ToString("N");
            var response = await _api.MarkVehicleExitAsync(target.VehicleId, key).ConfigureAwait(false);
            var toast = string.Format(CultureInfo.CurrentCulture, ExitToastExitMarkedFormat, target.PlateNumber);
            await _toast.ShowAsync(toast).ConfigureAwait(false);

            if (string.Equals(response.DeclarationStatus, "AllExited", StringComparison.OrdinalIgnoreCase))
            {
                await _toast.ShowAsync(AppResources.Exit_Toast_DeclarationAllExited).ConfigureAwait(false);
            }

            Suffix = string.Empty;
            Results.Clear();
            SelectedResult = null;
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.Exit_Error_MarkExit;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Exit_Error_MarkExit;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
