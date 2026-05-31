using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2.Desktop.ViewModels;

/// <summary>
/// Wireframe § 6.7 — Xuất Sổ B2 (F-13). Date-range presets + always-on "Đã bật"
/// badges (the three Sổ B2 transformations are runtime-fixed per FR-015 / UI Q3,
/// so they are read-only here, not checkboxes). Export downloads the template-based
/// .xlsx from the server and hands it to a native Save As dialog. Cancellable.
/// </summary>
public sealed partial class ExportViewModel : ObservableObject, IDisposable
{
    private static readonly CompositeFormat SuccessFormat = CompositeFormat.Parse(AppResources.Export_Success_Format);
    private static readonly CompositeFormat ErrorFormat = CompositeFormat.Parse(AppResources.Export_Error_Format);
    private static readonly CompositeFormat LastExportFormat = CompositeFormat.Parse(AppResources.Export_LastExport_Format);

    private readonly IB2Api _api;
    private readonly IToastService _toast;
    private CancellationTokenSource? _exportCts;

    public ExportViewModel(IB2Api api, IToastService toast)
    {
        _api = api;
        _toast = toast;
        var today = DateTime.Today;
        _customFrom = today;
        _customTo = today;
    }

    /// <summary>0 = today, 1 = yesterday, 2 = this week, 3 = this month, 4 = custom.</summary>
    [ObservableProperty]
    private int _datePresetIndex;

    [ObservableProperty]
    private DateTime _customFrom;

    [ObservableProperty]
    private DateTime _customTo;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string? _lastExportText;

    public IReadOnlyList<string> Presets { get; } = new[]
    {
        AppResources.Export_Preset_Today,
        AppResources.Export_Preset_Yesterday,
        AppResources.Export_Preset_ThisWeek,
        AppResources.Export_Preset_ThisMonth,
        AppResources.Export_Preset_Custom,
    };

    public bool IsCustomRange => DatePresetIndex == 4;

    partial void OnDatePresetIndexChanged(int value) => OnPropertyChanged(nameof(IsCustomRange));

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsExporting)
        {
            return;
        }

        var (from, to) = ResolveRange();
        if (from > to)
        {
            await _toast.ShowAsync(AppResources.Export_Error_InvalidRange, ToastSeverity.Warning).ConfigureAwait(true);
            return;
        }

        _exportCts?.Dispose();
        _exportCts = new CancellationTokenSource();
        var ct = _exportCts.Token;
        IsExporting = true;
        try
        {
            var fromStr = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var toStr = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            using var response = await _api.ExportSoB2Async(fromStr, toStr, ct).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                await _toast.ShowAsync(
                    string.Format(CultureInfo.CurrentCulture, ErrorFormat, (int)response.StatusCode),
                    ToastSeverity.Error).ConfigureAwait(true);
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(true);
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? string.Create(CultureInfo.InvariantCulture, $"SoB2_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");

            using var stream = new MemoryStream(bytes);
            var result = await FileSaver.Default.SaveAsync(fileName, stream, ct).ConfigureAwait(true);
            if (!result.IsSuccessful)
            {
                await _toast.ShowAsync(AppResources.Export_Cancelled, ToastSeverity.Warning).ConfigureAwait(true);
                return;
            }

            LastExportText = string.Format(CultureInfo.CurrentCulture, LastExportFormat, fileName);
            await _toast.ShowAsync(
                string.Format(CultureInfo.CurrentCulture, SuccessFormat, fileName),
                ToastSeverity.Success).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            await _toast.ShowAsync(AppResources.Export_Cancelled, ToastSeverity.Warning).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await _toast.ShowAsync(AppResources.Export_Error_Network, ToastSeverity.Error).ConfigureAwait(true);
        }
        catch (FileSaveException)
        {
            await _toast.ShowAsync(AppResources.Export_Error_Network, ToastSeverity.Error).ConfigureAwait(true);
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _exportCts?.Cancel();

    private (DateOnly From, DateOnly To) ResolveRange()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return DatePresetIndex switch
        {
            1 => (today.AddDays(-1), today.AddDays(-1)),
            2 => (StartOfWeek(today), today),
            3 => (new DateOnly(today.Year, today.Month, 1), today),
            4 => (DateOnly.FromDateTime(CustomFrom), DateOnly.FromDateTime(CustomTo)),
            _ => (today, today),
        };
    }

    // Week starts Monday (Vietnamese convention).
    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    public void Dispose() => _exportCts?.Dispose();
}
