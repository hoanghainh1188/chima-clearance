using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Reports;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

/// <summary>Wireframe § 6.6 — Báo cáo. Sortable column grids + copy/export per
/// FR-022 / T-S4-14. ClosedXML used for the simple per-section export
/// (NOT the F-13 Sổ B2 template).</summary>
public sealed partial class ReportsViewModel : ObservableObject
{
    private static readonly CompositeFormat ExportSuccessFormat = CompositeFormat.Parse(AppResources.Reports_Export_Success);
    private static readonly CompositeFormat ErrorFormat = CompositeFormat.Parse(AppResources.Reports_Error_Format);

    private readonly IB2Api _api;
    private readonly IToastService _toast;

    public ReportsViewModel(IB2Api api, IToastService toast)
    {
        _api = api;
        _toast = toast;
        var today = DateTime.Today;
        FromDate = today;
        ToDate = today;
    }

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    [ObservableProperty]
    private bool _includeCancelled;

    [ObservableProperty]
    private int _bucketMinutes = 60;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<ProcessorReportItemDto> Processors { get; } = new();

    public ObservableCollection<YardReportItemDto> Yards { get; } = new();

    public ObservableCollection<HourReportItemDto> Hours { get; } = new();

    public ObservableCollection<OfficerReportItemDto> Officers { get; } = new();

    public ObservableCollection<PendingReportItemDto> Pending { get; } = new();

    public bool HasNoData =>
        !IsLoading
        && string.IsNullOrEmpty(ErrorMessage)
        && Processors.Count == 0
        && Yards.Count == 0
        && Hours.Count == 0
        && Officers.Count == 0
        && Pending.Count == 0;

    private void NotifyEmpty() => OnPropertyChanged(nameof(HasNoData));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        ErrorMessage = null;
        IsLoading = true;
        NotifyEmpty();

        var from = DateOnly.FromDateTime(FromDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = DateOnly.FromDateTime(ToDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            var processor = await _api.GetByProcessorReportAsync(from, to, IncludeCancelled).ConfigureAwait(true);
            var yard = await _api.GetByYardReportAsync(from, to, IncludeCancelled).ConfigureAwait(true);
            var hour = await _api.GetByHourReportAsync(from, BucketMinutes, IncludeCancelled).ConfigureAwait(true);
            var officer = await _api.GetByOfficerReportAsync(from, to, IncludeCancelled).ConfigureAwait(true);
            var pending = await _api.GetPendingReportAsync(DateTimeOffset.UtcNow, IncludeCancelled).ConfigureAwait(true);

            Processors.Clear();
            foreach (var r in processor)
            {
                Processors.Add(r);
            }

            Yards.Clear();
            foreach (var r in yard)
            {
                Yards.Add(r);
            }

            Hours.Clear();
            foreach (var r in hour)
            {
                Hours.Add(r);
            }

            Officers.Clear();
            foreach (var r in officer)
            {
                Officers.Add(r);
            }

            Pending.Clear();
            foreach (var r in pending)
            {
                Pending.Add(r);
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, ErrorFormat, (int)ex.StatusCode);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Reports_Error_Network;
        }
        finally
        {
            IsLoading = false;
            NotifyEmpty();
        }
    }

    [RelayCommand]
    private void SortProcessor(string? column) => SortInPlace(Processors, column, ProcessorKey);

    [RelayCommand]
    private void SortYard(string? column) => SortInPlace(Yards, column, YardKey);

    [RelayCommand]
    private void SortHour(string? column) => SortInPlace(Hours, column, HourKey);

    [RelayCommand]
    private void SortOfficer(string? column) => SortInPlace(Officers, column, OfficerKey);

    [RelayCommand]
    private void SortPending(string? column) => SortInPlace(Pending, column, PendingKey);

    private static IComparable? ProcessorKey(ProcessorReportItemDto x, string? c) => c switch
    {
        "name" => x.ProcessorName,
        _ => x.TotalVehicles,
    };

    private static IComparable? YardKey(YardReportItemDto x, string? c) => c switch
    {
        "code" => x.YardCode,
        "name" => x.YardName,
        _ => x.TotalVehicles,
    };

    private static IComparable? HourKey(HourReportItemDto x, string? c) => c switch
    {
        "count" => x.ExitedCount,
        _ => x.BucketLabel,
    };

    private static IComparable? OfficerKey(OfficerReportItemDto x, string? c) => c switch
    {
        "name" => x.OfficerName,
        "kvgs" => x.DeclarationsKvgsConfirmed,
        _ => x.VehiclesEntered,
    };

    private static IComparable? PendingKey(PendingReportItemDto x, string? c) => c switch
    {
        "decl" => x.DeclarationNumber,
        "plate" => x.PlateNumber,
        "entry" => x.EntryTime,
        _ => x.HoursInside,
    };

    private readonly Dictionary<string, bool> _ascending = new();

    private void SortInPlace<T>(ObservableCollection<T> source, string? column, Func<T, string?, IComparable?> keyFn)
    {
        if (source.Count == 0)
        {
            return;
        }

        var asc = _ascending.GetValueOrDefault(typeof(T).FullName + ":" + (column ?? string.Empty));
        var ordered = (asc
            ? source.OrderByDescending(x => keyFn(x, column))
            : source.OrderBy(x => keyFn(x, column))).ToList();
        _ascending[typeof(T).FullName + ":" + (column ?? string.Empty)] = !asc;
        source.Clear();
        foreach (var item in ordered)
        {
            source.Add(item);
        }
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        var text = BuildPlainText();
        await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
        await _toast.ShowAsync(AppResources.Reports_Copy_Success, ToastSeverity.Success).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var fileName = $"B2-Reports-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            var path = Path.Combine(folder, fileName);
            using var workbook = new XLWorkbook();
            WriteSheet(workbook, AppResources.Reports_ByProcessor_Title, Processors,
                new[] { AppResources.Reports_Column_Processor, AppResources.Reports_Column_TotalVehicles },
                p => new object[] { p.ProcessorName, p.TotalVehicles });
            WriteSheet(workbook, AppResources.Reports_ByYard_Title, Yards,
                new[] { AppResources.Reports_Column_Yard, AppResources.Reports_Column_TotalVehicles },
                y => new object[] { $"{y.YardCode} — {y.YardName}", y.TotalVehicles });
            WriteSheet(workbook, AppResources.Reports_ByHour_Title, Hours,
                new[] { AppResources.Reports_Column_HourBucket, AppResources.Reports_Column_ExitedCount },
                h => new object[] { h.BucketLabel, h.ExitedCount });
            WriteSheet(workbook, AppResources.Reports_ByOfficer_Title, Officers,
                new[] { AppResources.Reports_Column_Officer, AppResources.Reports_Column_VehiclesEntered, AppResources.Reports_Column_KvgsConfirmed },
                o => new object[] { o.OfficerName, o.VehiclesEntered, o.DeclarationsKvgsConfirmed });
            WriteSheet(workbook, AppResources.Reports_Pending_Title, Pending,
                new[] { AppResources.Reports_Column_DeclarationNo, AppResources.Reports_Column_Plate, AppResources.Reports_Column_EntryTime, AppResources.Reports_Column_HoursInside },
                p => new object[] { p.DeclarationNumber, p.PlateNumber, p.EntryTime, p.HoursInside });
            workbook.SaveAs(path);
            await _toast.ShowAsync(string.Format(CultureInfo.CurrentCulture, ExportSuccessFormat, path), ToastSeverity.Success).ConfigureAwait(true);
        }
        catch (IOException ex)
        {
            await _toast.ShowAsync(ex.Message, ToastSeverity.Error).ConfigureAwait(true);
        }
    }

    private static void WriteSheet<T>(XLWorkbook workbook, string sheetName, IEnumerable<T> items, IReadOnlyList<string> headers, Func<T, object[]> rowFn)
    {
        // ClosedXML caps sheet names at 31 chars and disallows : / \ ? * [ ].
        var safeName = SanitizeSheetName(sheetName);
        var ws = workbook.Worksheets.Add(safeName);
        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var item in items)
        {
            var values = rowFn(item);
            for (var i = 0; i < values.Length; i++)
            {
                var cell = ws.Cell(r, i + 1);
                switch (values[i])
                {
                    case null:
                        cell.Value = Blank.Value;
                        break;
                    case int n:
                        cell.Value = n;
                        break;
                    case double d:
                        cell.Value = d;
                        break;
                    case DateTimeOffset dto:
                        cell.Value = dto.LocalDateTime;
                        break;
                    case DateTime dt:
                        cell.Value = dt;
                        break;
                    default:
                        cell.Value = values[i].ToString() ?? string.Empty;
                        break;
                }
            }

            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static string SanitizeSheetName(string raw)
    {
        var clean = new string(raw.Where(c => c is not (':' or '/' or '\\' or '?' or '*' or '[' or ']')).ToArray());
        return clean.Length > 31 ? clean[..31] : clean;
    }

    private string BuildPlainText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(AppResources.Reports_ByProcessor_Title);
        foreach (var p in Processors)
        {
            sb.Append(p.ProcessorName).Append('\t').Append(p.TotalVehicles).AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine(AppResources.Reports_ByYard_Title);
        foreach (var y in Yards)
        {
            sb.Append(y.YardCode).Append(" — ").Append(y.YardName).Append('\t').Append(y.TotalVehicles).AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine(AppResources.Reports_ByHour_Title);
        foreach (var h in Hours)
        {
            sb.Append(h.BucketLabel).Append('\t').Append(h.ExitedCount).AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine(AppResources.Reports_ByOfficer_Title);
        foreach (var o in Officers)
        {
            sb.Append(o.OfficerName).Append('\t').Append(o.VehiclesEntered).Append('\t').Append(o.DeclarationsKvgsConfirmed).AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine(AppResources.Reports_Pending_Title);
        foreach (var p in Pending)
        {
            sb.Append(p.DeclarationNumber).Append('\t').Append(p.PlateNumber).Append('\t')
                .Append(p.EntryTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append('\t')
                .Append(p.HoursInside.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
        }

        return sb.ToString();
    }
}
