using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using B2.Shared.Contracts.Users;
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
    private static readonly CompositeFormat RecomputeConfirmFormat = CompositeFormat.Parse(AppResources.Ledger_Recompute_ConfirmMessage);
    private static readonly CompositeFormat RecomputeSuccessFormat = CompositeFormat.Parse(AppResources.Ledger_Recompute_Success);
    private static readonly CompositeFormat RecomputeErrorFormat = CompositeFormat.Parse(AppResources.Ledger_Recompute_ErrorFormat);

    private readonly IB2Api _api;
    private readonly ConnectivityService _connectivity;
    private readonly IModalService _modalService;
    private readonly IToastService _toastService;
    private readonly ISessionContext _session;
    private DateTimeOffset _sinceWatermark = DateTimeOffset.UtcNow.AddMinutes(-30);
    private bool _disposed;
    private bool _filterEventsEnabled = true;
    private readonly Dictionary<DateOnly, string?> _officerByDate = new();

    /// <summary>
    /// Surfaced for XAML binding so the error banner (wireframe § 8) can
    /// show "Mã lỗi: {CurrentCorrelationId}". Same singleton the status
    /// bar binds to.
    /// </summary>
    public ISessionCorrelationService Correlation { get; }

    public LedgerViewModel(
        IB2Api api,
        ConnectivityService connectivity,
        ISessionCorrelationService correlation,
        IModalService modalService,
        IToastService toastService,
        ISessionContext session)
    {
        _filterEventsEnabled = false;
        _api = api;
        _connectivity = connectivity;
        _modalService = modalService;
        _toastService = toastService;
        _session = session;
        _session.PropertyChanged += OnSessionChanged;
        Correlation = correlation;
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
        _connectivity.DailyHeaderOfficerChanged += OnDailyHeaderOfficerPush;
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

    public bool IsAdmin => _session.Role == UserRoleDto.Admin;

    private void OnSessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ISessionContext.Role))
        {
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

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

    /// <summary>
    /// Wireframe § 6.2 right-side detail drawer. Populated by
    /// <see cref="OpenDetailCommand"/> on a row double-tap and cleared by
    /// <see cref="CloseDetailCommand"/> (also bound to the desktop Esc shortcut).
    /// </summary>
    [ObservableProperty]
    private DeclarationDetailResponse? _selectedDeclarationDetail;

    [ObservableProperty]
    private bool _isDrawerOpen;

    [ObservableProperty]
    private bool _isDrawerLoading;

    [ObservableProperty]
    private string? _drawerErrorMessage;

    public bool HasDrawerData =>
        !IsDrawerLoading && string.IsNullOrEmpty(DrawerErrorMessage) && SelectedDeclarationDetail is not null;

    partial void OnSelectedDeclarationDetailChanged(DeclarationDetailResponse? value) =>
        OnPropertyChanged(nameof(HasDrawerData));

    partial void OnIsDrawerLoadingChanged(bool value) => OnPropertyChanged(nameof(HasDrawerData));

    partial void OnDrawerErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasDrawerData));

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

    private void OnDailyHeaderOfficerPush(DateOnly date, string officerName)
    {
        _officerByDate[date] = officerName;
        foreach (var item in DisplayItems)
        {
            if (item.IsDateHeader && item.HeaderDate == date)
            {
                item.OfficerName = officerName;
            }
        }
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

    [RelayCommand]
    private async Task OpenDetailAsync(LedgerRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        DrawerErrorMessage = null;
        IsDrawerOpen = true;
        IsDrawerLoading = true;
        SelectedDeclarationDetail = null;

        try
        {
            SelectedDeclarationDetail = await _api
                .GetDeclarationDetailAsync(row.Dto.Id)
                .ConfigureAwait(true);
        }
        catch (ApiException)
        {
            DrawerErrorMessage = AppResources.Ledger_Drawer_LoadError;
        }
        catch (HttpRequestException)
        {
            DrawerErrorMessage = AppResources.Ledger_Drawer_LoadError;
        }
        finally
        {
            IsDrawerLoading = false;
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        IsDrawerOpen = false;
        SelectedDeclarationDetail = null;
        DrawerErrorMessage = null;
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

    private static void RebuildDisplayItems(
        IReadOnlyList<DeclarationSummaryDto> items,
        int page,
        int pageSize,
        Dictionary<DateOnly, string?> officerByDate,
        ObservableCollection<LedgerUiItem> target)
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
            var officer = officerByDate.TryGetValue(g.Key, out var name) ? name : null;
            var header = string.Format(CultureInfo.CurrentCulture, DailyHeaderFormat, dateStr, officer ?? string.Empty);
            target.Add(LedgerUiItem.ForHeader(header, g.Key, officer));
            foreach (var dto in g.OrderByDescending(x => x.EnteredAt))
            {
                ordinal++;
                target.Add(LedgerUiItem.ForRow(LedgerRowViewModel.FromDto(dto, ordinal)));
            }
        }
    }

    [RelayCommand]
    private async Task RecomputeDailySummaryAsync(LedgerUiItem? item)
    {
        if (item?.HeaderDate is null || !IsAdmin)
        {
            return;
        }

        var date = item.HeaderDate.Value;
        var dateStr = date.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
        var confirmed = await _modalService.ConfirmAsync(
                AppResources.Ledger_Recompute_ConfirmTitle,
                string.Format(CultureInfo.CurrentCulture, RecomputeConfirmFormat, dateStr),
                AppResources.Common_Yes,
                AppResources.Common_No)
            .ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _api.RecomputeDailySummaryAsync(
                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    new RecomputeDailySummaryRequest("Admin recompute"),
                    CancellationToken.None)
                .ConfigureAwait(true);
            await _toastService.ShowAsync(
                    string.Format(CultureInfo.CurrentCulture, RecomputeSuccessFormat, dateStr),
                    ToastSeverity.Success)
                .ConfigureAwait(true);
        }
        catch (ApiException ex)
        {
            await _toastService.ShowAsync(
                    string.Format(CultureInfo.CurrentCulture, RecomputeErrorFormat, (int)ex.StatusCode),
                    ToastSeverity.Error)
                .ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await _toastService.ShowAsync(AppResources.Ledger_Error_Network, ToastSeverity.Error).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task SaveOfficerNameAsync(LedgerUiItem? item)
    {
        if (item?.HeaderDate is null || string.IsNullOrWhiteSpace(item.OfficerName))
        {
            return;
        }

        var date = item.HeaderDate.Value;
        var newValue = item.OfficerName.Trim();
        var previous = _officerByDate.TryGetValue(date, out var prev) ? prev : null;

        // Optimistic: keep new value on the UI; cache it so other refreshes see it.
        _officerByDate[date] = newValue;
        item.OfficerName = newValue;

        try
        {
            await _api.UpdateOfficerNameAsync(
                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    new UpdateOfficerNameRequest(newValue),
                    CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (ApiException ex)
        {
            // Revert: restore previous officer name on failure.
            _officerByDate[date] = previous;
            item.OfficerName = previous;
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, ErrorLoadFormat, (int)ex.StatusCode);
            NotifyViewStates();
        }
        catch (HttpRequestException)
        {
            _officerByDate[date] = previous;
            item.OfficerName = previous;
            ErrorMessage = AppResources.Ledger_Error_Network;
            NotifyViewStates();
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

            try
            {
                var headers = await _api.ListDailyHeadersAsync(q.from, q.to).ConfigureAwait(true);
                foreach (var h in headers)
                {
                    _officerByDate[h.Date] = h.OnDutyOfficerName;
                }
            }
            catch (ApiException) { /* officer-name overlay is best-effort */ }
            catch (HttpRequestException) { }

            DisplayItems.Clear();
            RebuildDisplayItems(response.Items, Page, PageSize, _officerByDate, DisplayItems);

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
        _connectivity.DailyHeaderOfficerChanged -= OnDailyHeaderOfficerPush;
        _connectivity.ModeChanged -= OnConnectivityChanged;
        _session.PropertyChanged -= OnSessionChanged;
    }
}
