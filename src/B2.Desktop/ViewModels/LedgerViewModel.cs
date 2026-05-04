using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public sealed partial class LedgerViewModel : ObservableObject, IDisposable
{
    private static readonly CompositeFormat PageSummaryFormat = CompositeFormat.Parse(AppResources.Ledger_PageSummary_Format);
    private static readonly CompositeFormat DisplayRangeFormat = CompositeFormat.Parse(AppResources.Ledger_DisplayRange_Format);
    private static readonly CompositeFormat SubtitleFormat = CompositeFormat.Parse(AppResources.Ledger_Subtitle_Format);
    private static readonly CompositeFormat DailyHeaderFormat = CompositeFormat.Parse(AppResources.Ledger_DailyHeader_Format);
    private static readonly CompositeFormat ErrorLoadFormat = CompositeFormat.Parse(AppResources.Ledger_Error_Load_Format);

    private readonly IB2Api _api;
    private readonly ConnectivityService _connectivity;
    private DateTimeOffset _sinceWatermark = DateTimeOffset.UtcNow.AddMinutes(-30);
    private bool _disposed;
    private bool _filterEventsEnabled = true;

    public LedgerViewModel(IB2Api api, ConnectivityService connectivity)
    {
        _filterEventsEnabled = false;
        _api = api;
        _connectivity = connectivity;
        DatePresetOptions = new[]
        {
            AppResources.Ledger_Date_Today,
            AppResources.Ledger_Date_Last7Days,
            AppResources.Ledger_Date_Last30Days,
        };
        StatusFilterOptions = new[]
        {
            AppResources.Ledger_Filter_Status_All,
            AppResources.Ledger_Filter_Status_Open,
            AppResources.Ledger_Filter_Status_AllExited,
            AppResources.Ledger_Filter_Status_KvgsConfirmed,
            AppResources.Ledger_Filter_Status_Cancelled,
        };
        _connectivity.SuggestedListRefresh += OnSuggestedRefresh;
        _connectivity.DeclarationLedgerChanged += OnLedgerPush;
        _connectivity.ModeChanged += OnConnectivityChanged;
        DisplayItems.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(NotifyViewStates);
        OnConnectivityChanged(this, EventArgs.Empty);
        UpdateLedgerSubtitle();
        RebuildYardPickerSource();
        SelectedYard = YardPickerSource[0];
        _filterEventsEnabled = true;
    }

    public IReadOnlyList<string> DatePresetOptions { get; }

    public IReadOnlyList<string> StatusFilterOptions { get; }

    public ObservableCollection<YardDto> YardPickerSource { get; } = new();

    [ObservableProperty]
    private int _datePresetIndex;

    [ObservableProperty]
    private int _statusFilterIndex;

    [ObservableProperty]
    private YardDto? _selectedYard;

    [ObservableProperty]
    private string _plateSearch = string.Empty;

    [ObservableProperty]
    private bool _filterOnlyUnexited;

    [ObservableProperty]
    private IReadOnlyList<YardDto> _yards = Array.Empty<YardDto>();

    [ObservableProperty]
    private string _ledgerSubtitle = string.Empty;

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

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = 50;

    public string PageSummary =>
        string.Format(
            CultureInfo.CurrentCulture,
            PageSummaryFormat,
            Page,
            Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize))));

    public string DisplayRangeText
    {
        get
        {
            if (TotalCount <= 0)
            {
                return string.Empty;
            }

            var start = (Page - 1) * PageSize + 1;
            var end = Math.Min(Page * PageSize, TotalCount);
            return string.Format(CultureInfo.CurrentCulture, DisplayRangeFormat, start, end, TotalCount);
        }
    }

    public ObservableCollection<LedgerUiItem> DisplayItems { get; } = new();

    public bool ShowEmpty => !IsLoading && string.IsNullOrEmpty(ErrorMessage) && TotalCount == 0;

    public bool ShowDataGrid => !IsLoading && string.IsNullOrEmpty(ErrorMessage) && TotalCount > 0;

    private void NotifyViewStates()
    {
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowDataGrid));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(DisplayRangeText));
    }

    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(DisplayRangeText));
    }

    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(DisplayRangeText));
    }

    partial void OnPageSizeChanged(int value)
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(DisplayRangeText));
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
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(useSince: true).ConfigureAwait(true));
    }

    private void OnLedgerPush(Guid declarationId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _ = declarationId;
            await LoadAsync(useSince: false).ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task Init()
    {
        _filterEventsEnabled = false;
        if (Yards.Count == 0)
        {
            try
            {
                var y = await _api.ListYardsAsync(includeInactive: false, page: 1, pageSize: 500).ConfigureAwait(true);
                Yards = y.Items;
                RebuildYardPickerSource();
                SelectedYard ??= YardPickerSource[0];
            }
            catch (HttpRequestException)
            {
                // Yard filter optional; ledger still loads.
            }
            catch (ApiException)
            {
            }
        }

        _filterEventsEnabled = true;
        Page = 1;
        await LoadAsync(useSince: false).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(useSince: false);

    [RelayCommand]
    private async Task SearchFromFilterAsync()
    {
        Page = 1;
        await LoadAsync(useSince: false).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        _filterEventsEnabled = false;
        DatePresetIndex = 0;
        StatusFilterIndex = 0;
        PlateSearch = string.Empty;
        FilterOnlyUnexited = false;
        SelectedYard = YardPickerSource.Count > 0 ? YardPickerSource[0] : null;
        _filterEventsEnabled = true;
        Page = 1;
        await LoadAsync(useSince: false).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page * PageSize >= TotalCount)
        {
            return;
        }

        Page++;
        await LoadAsync(useSince: false).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (Page <= 1)
        {
            return;
        }

        Page--;
        await LoadAsync(useSince: false).ConfigureAwait(true);
    }

    partial void OnDatePresetIndexChanged(int value) => _ = FilterPresetChangedAsync();

    partial void OnStatusFilterIndexChanged(int value) => _ = FilterPresetChangedAsync();

    partial void OnFilterOnlyUnexitedChanged(bool value) => _ = FilterPresetChangedAsync();

    partial void OnSelectedYardChanged(YardDto? value) => _ = FilterPresetChangedAsync();

    private async Task FilterPresetChangedAsync()
    {
        if (!_filterEventsEnabled)
        {
            return;
        }

        Page = 1;
        await LoadAsync(useSince: false).ConfigureAwait(true);
    }

    private void RebuildYardPickerSource()
    {
        YardPickerSource.Clear();
        YardPickerSource.Add(new YardDto(Guid.Empty, string.Empty, AppResources.Ledger_YardFilter_All, true));
        foreach (var y in Yards)
        {
            YardPickerSource.Add(y);
        }
    }

    private void UpdateLedgerSubtitle()
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        LedgerSubtitle = string.Format(
            CultureInfo.CurrentCulture,
            SubtitleFormat,
            VietnamToday().ToString("dd/MM/yyyy", culture));
    }

    private static DateOnly VietnamToday()
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateOnly.FromDateTime(local);
    }

    private (string from, string to, string? status, bool includeCancelled, string? plate, string? yardIds) BuildQuery()
    {
        var today = VietnamToday();
        DateOnly from;
        DateOnly to;
        switch (DatePresetIndex)
        {
            case 1:
                from = today.AddDays(-6);
                to = today;
                break;
            case 2:
                from = today.AddDays(-29);
                to = today;
                break;
            default:
                from = today;
                to = today;
                break;
        }

        var includeCancelled = StatusFilterIndex == 4;
        string? status = StatusFilterIndex switch
        {
            1 => "Open",
            2 => "AllExited",
            3 => "KVGSConfirmed",
            4 => "Cancelled",
            _ => null,
        };

        if (FilterOnlyUnexited && StatusFilterIndex != 4)
        {
            status = "Open";
        }

        var plate = string.IsNullOrWhiteSpace(PlateSearch) ? null : PlateSearch.Trim();
        string? yardIds = null;
        if (SelectedYard is not null && SelectedYard.Id != Guid.Empty)
        {
            yardIds = SelectedYard.Id.ToString();
        }

        return (
            from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            status,
            includeCancelled,
            plate,
            yardIds);
    }

    private static void RebuildDisplayItems(IReadOnlyList<DeclarationSummaryDto> items, int page, int pageSize, ObservableCollection<LedgerUiItem> target)
    {
        target.Clear();
        if (items.Count == 0)
        {
            return;
        }

        var grouped = items
            .GroupBy(x => x.BusinessDate)
            .OrderByDescending(g => g.Max(x => x.EnteredAt))
            .ToList();

        var ordinal = (page - 1) * pageSize;
        foreach (var g in grouped)
        {
            var culture = CultureInfo.GetCultureInfo("vi-VN");
            var dateStr = g.Key.ToString("dd/MM/yyyy", culture);
            var officer = g.OrderBy(x => x.EnteredAt).First().ProcessorName;
            var header = string.Format(CultureInfo.CurrentCulture, DailyHeaderFormat, dateStr, officer);
            target.Add(LedgerUiItem.ForHeader(header));
            foreach (var dto in g.OrderByDescending(x => x.EnteredAt))
            {
                ordinal++;
                target.Add(LedgerUiItem.ForRow(LedgerRowViewModel.FromDto(dto, ordinal)));
            }
        }
    }

    private async Task LoadAsync(bool useSince)
    {
        if (IsLoading)
        {
            return;
        }

        ErrorMessage = null;
        IsLoading = true;
        UpdateLedgerSubtitle();
        NotifyViewStates();
        try
        {
            string? sinceParam = null;
            if (useSince)
            {
                sinceParam = _sinceWatermark.ToString("O", CultureInfo.InvariantCulture);
            }

            var q = BuildQuery();
            var response = await _api.ListDeclarationsAsync(
                    Page,
                    PageSize,
                    q.status,
                    sinceParam,
                    q.from,
                    q.to,
                    q.plate,
                    q.yardIds,
                    q.includeCancelled ? true : null)
                .ConfigureAwait(true);
            TotalCount = response.TotalCount;
            PageSize = response.PageSize;
            Page = response.Page;
            DisplayItems.Clear();
            RebuildDisplayItems(response.Items, Page, PageSize, DisplayItems);

            if (response.Items.Count > 0)
            {
                _sinceWatermark = response.Items.Max(x => x.EnteredAt);
            }

            NotifyViewStates();
        }
        catch (ApiException ex)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, ErrorLoadFormat, (int)ex.StatusCode);
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
