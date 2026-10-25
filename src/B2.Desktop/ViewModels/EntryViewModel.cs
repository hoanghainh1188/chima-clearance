using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public partial class EntryViewModel : ObservableObject, IDisposable
{
    private static readonly Regex DeclNumber = new("^[0-9]{12}$", RegexOptions.Compiled);

    /// <summary>Mirrors the server-side ceiling (DeclarationCreateRequestValidator).</summary>
    private const int MaxDeclarationNumbers = 20;

    /// <summary>
    /// Cột C may hold several numbers in one cell, one per line — the Excel sheet used Alt+Enter.
    /// Commas and semicolons are accepted too, since a paste from another system may use them.
    /// </summary>
    private static List<string> ParseDeclarationNumbers(string? raw) =>
        (raw ?? string.Empty)
            .Split(
                ['\n', '\r', ',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static readonly Regex Plate = new("^[0-9]{2}[A-Za-z]{1,2}[0-9]{4,5}$", RegexOptions.Compiled);

    /// <summary>
    /// Cột E/F: upper-case and strip separators so a plate pasted from the unit's internal system
    /// ("29C-12345") is accepted, exactly like <c>PlateNumber.Normalize</c> server-side. The rule
    /// is duplicated because B2.Desktop may only reference B2.Shared.Contracts (Constitution V).
    /// </summary>
    private static string NormalizePlate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        var buffer = new char[trimmed.Length];
        var written = 0;
        foreach (var ch in trimmed)
        {
            if (Array.IndexOf(PlateSeparators, ch) < 0)
            {
                buffer[written++] = ch;
            }
        }

        return new string(buffer, 0, written);
    }

    private static readonly char[] PlateSeparators = [' ', ' ', '-', '–', '.', '_', '/'];
    private static readonly CompositeFormat AddChildBannerFormat = CompositeFormat.Parse(AppResources.Entry_AddChildBanner_Format);

    private readonly IB2Api _api;
    private readonly IToastService _toast;

    // F-17 Smart Suggestions
    private readonly SuggestionLatencyGuard _suggestionGuard = new(TimeProvider.System);
    private CancellationTokenSource? _suggestCts;

    /// <summary>Raised after a save / cancel clears the form, so the view can refocus the first field.</summary>
    public event EventHandler? FormCleared;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private IReadOnlyList<ProcessorDto> _processors = Array.Empty<ProcessorDto>();

    [ObservableProperty]
    private IReadOnlyList<YardDto> _yards = Array.Empty<YardDto>();

    [ObservableProperty]
    private ProcessorDto? _selectedProcessor;

    [ObservableProperty]
    private YardDto? _selectedYard;

    [ObservableProperty]
    private string _declarationNumber = string.Empty;

    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _goodsName = string.Empty;

    [ObservableProperty]
    private string _plateNumber = string.Empty;

    [ObservableProperty]
    private string? _trailerOrContainerNumber;

    // F-17: ghost-text suggestions
    [ObservableProperty]
    private string? _suggestedTrailer;

    [ObservableProperty]
    private string? _suggestedCompany;

    [ObservableProperty]
    private string? _suggestedGoods;

    [ObservableProperty]
    private bool _hasSuggestions;

    [ObservableProperty]
    private string? _vehicleNote;

    [ObservableProperty]
    private bool _isAddChildMode;

    [ObservableProperty]
    private Guid? _activeDeclarationId;

    [ObservableProperty]
    private string _addChildBannerDeclarationLabel = string.Empty;

    public EntryViewModel(IB2Api api, IToastService toast)
    {
        _api = api;
        _toast = toast;
    }

    // F-17: triggered by CommunityToolkit.Mvvm whenever PlateNumber changes.
    partial void OnPlateNumberChanged(string value) => _ = FetchSuggestionsAsync(value);

    // F-17: Tab key handler — copy suggestions into the actual fields.
    [RelayCommand]
    private void AcceptSuggestion()
    {
        if (!HasSuggestions)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TrailerOrContainerNumber) && !string.IsNullOrWhiteSpace(SuggestedTrailer))
        {
            TrailerOrContainerNumber = SuggestedTrailer;
        }

        if (string.IsNullOrWhiteSpace(CompanyName) && !string.IsNullOrWhiteSpace(SuggestedCompany))
        {
            CompanyName = SuggestedCompany;
        }

        if (string.IsNullOrWhiteSpace(GoodsName) && !string.IsNullOrWhiteSpace(SuggestedGoods))
        {
            GoodsName = SuggestedGoods;
        }

        ClearSuggestions();
    }

    private async Task FetchSuggestionsAsync(string plate)
    {
        ClearSuggestions();

        if (!_suggestionGuard.CanQuery() || !Plate.IsMatch(NormalizePlate(plate)))
        {
            return;
        }

        // Cancel any in-flight request for a previous plate value.
        _suggestCts?.Cancel();
        _suggestCts?.Dispose();
        _suggestCts = new CancellationTokenSource();
        var ct = _suggestCts.Token;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var plateNormalized = plate.Trim().ToUpperInvariant();
            var trailerTask = _api.SuggestVehiclePairAsync(plateNormalized, "trailer", ct);
            var companyTask = _api.SuggestVehiclePairAsync(plateNormalized, "company", ct);
            var goodsTask = _api.SuggestVehiclePairAsync(plateNormalized, "goods", ct);

            await Task.WhenAll(trailerTask, companyTask, goodsTask).ConfigureAwait(true);
            sw.Stop();

            if (ct.IsCancellationRequested)
            {
                return;
            }

            // Latency guard: 2 consecutive overruns hide the zone for a cooldown (#160).
            // INFO log is emitted by the server; desktop just hides the zone.
            if (_suggestionGuard.RecordRoundTrip(sw.Elapsed))
            {
                return;
            }

            var trailer = trailerTask.Result.Value;
            var company = companyTask.Result.Value;
            var goods = goodsTask.Result.Value;

            if (!string.IsNullOrWhiteSpace(trailer) || !string.IsNullOrWhiteSpace(company) || !string.IsNullOrWhiteSpace(goods))
            {
                SuggestedTrailer = trailer;
                SuggestedCompany = company;
                SuggestedGoods = goods;
                HasSuggestions = true;
            }
        }
        catch (OperationCanceledException)
        {
            // Stale request — ignore.
        }
        catch (Exception)
        {
            // Network errors silently suppress suggestions; never block the form.
        }
    }

    private void ClearSuggestions()
    {
        SuggestedTrailer = null;
        SuggestedCompany = null;
        SuggestedGoods = null;
        HasSuggestions = false;
    }

    public void Dispose()
    {
        _suggestCts?.Cancel();
        _suggestCts?.Dispose();
        _suggestCts = null;
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    public async Task InitAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var processorsTask = _api.ListProcessorsAsync(includeInactive: false, page: 1, pageSize: 500);
            var yardsTask = _api.ListYardsAsync(includeInactive: false, page: 1, pageSize: 500);
            await Task.WhenAll(processorsTask, yardsTask).ConfigureAwait(true);

            // Replacing ItemsSource clears the Picker selection, so a half-filled form keeps its
            // choice by Id. A processor/yard deactivated meanwhile drops out and must be re-picked.
            var processorId = SelectedProcessor?.Id;
            var yardId = SelectedYard?.Id;
            Processors = (await processorsTask.ConfigureAwait(true)).Items;
            Yards = (await yardsTask.ConfigureAwait(true)).Items;
            SelectedProcessor = Processors.FirstOrDefault(x => x.Id == processorId);
            SelectedYard = Yards.FirstOrDefault(x => x.Id == yardId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Entry_Error_LoadMaster;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Exits "add another vehicle" mode (F-06 banner — wireframe § 6.3).
    /// </summary>
    [RelayCommand]
    private void SwitchDeclaration() => ResetEntireForm();

    [RelayCommand]
    private async Task SaveAsync() => await SubmitAsync(saveAndContinue: false).ConfigureAwait(true);

    [RelayCommand]
    private async Task SaveAndContinueAsync() => await SubmitAsync(saveAndContinue: true).ConfigureAwait(true);

    [RelayCommand]
    private void Cancel()
    {
        ErrorMessage = null;
        ResetEntireForm();
    }

    private async Task SubmitAsync(bool saveAndContinue)
    {
        ErrorMessage = null;
        var plate = NormalizePlate(PlateNumber);
        PlateNumber = plate;
        if (!string.IsNullOrWhiteSpace(TrailerOrContainerNumber))
        {
            TrailerOrContainerNumber = NormalizePlate(TrailerOrContainerNumber);
        }

        if (IsAddChildMode && ActiveDeclarationId is Guid existingId)
        {
            if (!Plate.IsMatch(NormalizePlate(plate)))
            {
                ErrorMessage = AppResources.Entry_Error_PlateInvalid;
                return;
            }

            try
            {
                var key = Guid.NewGuid().ToString("N");
                await _api
                    .AddVehicleToDeclarationAsync(
                        existingId,
                        new VehicleAddRequest(plate, NullIfWhiteSpace(TrailerOrContainerNumber), NullIfWhiteSpace(VehicleNote)),
                        key)
                    .ConfigureAwait(true);
                ClearVehicleFields();
                await _toast.ShowAsync(AppResources.Entry_Toast_VehicleAdded, ToastSeverity.Success).ConfigureAwait(true);
            }
            catch (ApiException)
            {
                ErrorMessage = AppResources.Entry_Error_SaveVehicle;
            }
            catch (HttpRequestException)
            {
                ErrorMessage = AppResources.Entry_Error_SaveVehicle;
            }

            return;
        }

        if (!ValidateDeclarationForm(plate, out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        if (SelectedProcessor is null || SelectedYard is null)
        {
            ErrorMessage = AppResources.Entry_Error_MasterRequired;
            return;
        }

        var declNumbers = ParseDeclarationNumbers(DeclarationNumber);
        var firstVehicle = new VehicleAddRequest(
            plate,
            NullIfWhiteSpace(TrailerOrContainerNumber),
            NullIfWhiteSpace(VehicleNote));

        var request = new DeclarationCreateRequest(
            declNumbers,
            SelectedProcessor.Id,
            CompanyName.Trim(),
            GoodsName.Trim(),
            SelectedYard.Id,
            firstVehicle);

        try
        {
            var idem = Guid.NewGuid().ToString("N");
            var dto = await _api.CreateDeclarationAsync(request, idem).ConfigureAwait(true);
            if (saveAndContinue)
            {
                IsAddChildMode = true;
                ActiveDeclarationId = dto.Id;
                // The banner names the row being added to; the primary number identifies it.
                AddChildBannerDeclarationLabel = string.Format(
                    CultureInfo.CurrentCulture,
                    AddChildBannerFormat,
                    MaskDeclaration(dto.DeclarationNumber));
                ClearVehicleFields();
            }
            else
            {
                ResetEntireForm();
            }

            await _toast.ShowAsync(AppResources.Entry_Toast_Saved, ToastSeverity.Success).ConfigureAwait(true);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ApiErrorCode.FromProblemJson(ex.Content) == ApiErrorCode.DeclarationNumberDuplicate
                ? AppResources.Entry_Error_DuplicateNumber
                : AppResources.Entry_Error_CreateDeclaration;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Common_Error_ServerUnreachable;
        }
    }

    private bool ValidateDeclarationForm(string plate, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        var declNumbers = ParseDeclarationNumbers(DeclarationNumber);
        if (declNumbers.Count == 0 || declNumbers.Exists(n => !DeclNumber.IsMatch(n)))
        {
            error = AppResources.Entry_Error_DeclarationInvalid;
            return false;
        }

        if (declNumbers.Distinct(StringComparer.Ordinal).Count() != declNumbers.Count)
        {
            error = AppResources.Entry_Error_DeclarationDuplicate;
            return false;
        }

        if (declNumbers.Count > MaxDeclarationNumbers)
        {
            error = AppResources.Entry_Error_DeclarationTooMany;
            return false;
        }

        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            error = AppResources.Entry_Error_CompanyRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(GoodsName))
        {
            error = AppResources.Entry_Error_GoodsRequired;
            return false;
        }

        if (!Plate.IsMatch(NormalizePlate(plate)))
        {
            error = AppResources.Entry_Error_PlateInvalid;
            return false;
        }

        error = null;
        return true;
    }

    private void ClearVehicleFields()
    {
        PlateNumber = string.Empty;
        TrailerOrContainerNumber = null;
        VehicleNote = null;
        ClearSuggestions();
        FormCleared?.Invoke(this, EventArgs.Empty);
    }

    private void ResetEntireForm()
    {
        IsAddChildMode = false;
        ActiveDeclarationId = null;
        AddChildBannerDeclarationLabel = string.Empty;
        DeclarationNumber = string.Empty;
        CompanyName = string.Empty;
        GoodsName = string.Empty;
        SelectedProcessor = null;
        SelectedYard = null;
        ClearVehicleFields();
    }

    private static string? NullIfWhiteSpace(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static string MaskDeclaration(string twelveDigits)
    {
        var d = twelveDigits.Trim();
        return d.Length == 12 ? $"{d[..4]}…{d[^2..]}" : d;
    }
}
