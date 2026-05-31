using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Admin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

/// <summary>
/// Wireframe § 6.11 — Sức khỏe hệ thống (F-18), tabbed: Tổng quan (health checks) and
/// Nhật ký (recent log lines, newest-first, filterable by level). Admin only.
/// </summary>
public sealed partial class HealthViewModel : ObservableObject
{
    private static readonly CompositeFormat UptimeFormat = CompositeFormat.Parse(AppResources.Health_Uptime_Format);
    private static readonly CompositeFormat OpenOldFormat = CompositeFormat.Parse(AppResources.Health_OpenOld_Format);
    private static readonly CompositeFormat PageSummaryFormat = CompositeFormat.Parse(AppResources.Health_PageSummary_Format);
    private static readonly CompositeFormat ErrorFormat = CompositeFormat.Parse(AppResources.Health_Error_Format);

    private const int PageSizeValue = 100;

    private readonly IB2Api _api;

    public HealthViewModel(IB2Api api) => _api = api;

    [ObservableProperty]
    private int _selectedTab; // 0 = overview, 1 = logs

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private HealthReportDto? _report;

    [ObservableProperty]
    private int _levelIndex;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _total;

    public ObservableCollection<LogEntryDto> Logs { get; } = new();

    public IReadOnlyList<string> LevelOptions { get; } = new[]
    {
        AppResources.Health_Logs_LevelAll, "Information", "Warning", "Error",
    };

    public bool IsOverview => SelectedTab == 0;

    public bool IsLogs => SelectedTab == 1;

    public bool HasNoLogs => !IsLoading && Logs.Count == 0;

    public string UptimeText => Report is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, UptimeFormat, Report.UptimeSeconds / 3600, Report.UptimeSeconds % 3600 / 60);

    public string MemoryText => Report is null ? string.Empty : FormatBytes(Report.WorkingSetBytes);

    public string DbSizeText => Report is null ? string.Empty : FormatBytes(Report.DbFileBytes);

    public string FreeDiskText => Report is null ? string.Empty : FormatBytes(Report.FreeDiskBytes);

    public string OpenOldText => Report is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, OpenOldFormat, Report.OpenDeclarationsOlderThan24h);

    public string DiskStatus => StatusLabel(Report?.DiskSpaceWarning == true);

    public string OpenOldStatus => StatusLabel(Report?.OpenDeclarationsWarning == true);

    public string PageSummary => string.Format(CultureInfo.CurrentCulture, PageSummaryFormat, Logs.Count, Total);

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(IsLogs));
    }

    partial void OnReportChanged(HealthReportDto? value)
    {
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(DbSizeText));
        OnPropertyChanged(nameof(FreeDiskText));
        OnPropertyChanged(nameof(OpenOldText));
        OnPropertyChanged(nameof(DiskStatus));
        OnPropertyChanged(nameof(OpenOldStatus));
    }

    [RelayCommand]
    private void ShowOverview() => SelectedTab = 0;

    [RelayCommand]
    private async Task ShowLogsAsync()
    {
        SelectedTab = 1;
        if (Logs.Count == 0)
        {
            await LoadLogsAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task LoadHealthAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Report = await _api.GetHealthDetailAsync().ConfigureAwait(true);
        }
        catch (ApiException ex)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, ErrorFormat, (int)ex.StatusCode);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Health_Error_Network;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasNoLogs));
        try
        {
            var level = LevelIndex > 0 ? LevelOptions[LevelIndex] : null;
            var response = await _api.ListLogsAsync(level, Page, PageSizeValue).ConfigureAwait(true);
            Logs.Clear();
            foreach (var item in response.Items)
            {
                Logs.Add(item);
            }

            Total = response.Total;
            OnPropertyChanged(nameof(PageSummary));
        }
        catch (ApiException ex)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, ErrorFormat, (int)ex.StatusCode);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Health_Error_Network;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoLogs));
        }
    }

    [RelayCommand]
    private async Task ApplyLogFilterAsync()
    {
        Page = 1;
        await LoadLogsAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page * PageSizeValue >= Total)
        {
            return;
        }

        Page++;
        await LoadLogsAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (Page <= 1)
        {
            return;
        }

        Page--;
        await LoadLogsAsync().ConfigureAwait(true);
    }

    private static string StatusLabel(bool warning) =>
        warning ? AppResources.Health_Status_Warn : AppResources.Health_Status_Ok;

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1L << 30)
        {
            return (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }

        if (bytes >= 1L << 20)
        {
            return (bytes / (double)(1L << 20)).ToString("0", CultureInfo.InvariantCulture) + " MB";
        }

        return (bytes / (double)(1L << 10)).ToString("0", CultureInfo.InvariantCulture) + " KB";
    }
}
