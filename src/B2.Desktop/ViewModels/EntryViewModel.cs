using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

public partial class EntryViewModel : ObservableObject
{
    private static readonly Regex DeclNumber = new("^[0-9]{12}$", RegexOptions.Compiled);
    private static readonly Regex Plate = new("^[0-9]{2}[A-Za-z]{1,2}[0-9]{4,5}$", RegexOptions.Compiled);
    private static readonly CompositeFormat AddChildBannerFormat = CompositeFormat.Parse(AppResources.Entry_AddChildBanner_Format);

    private readonly IB2Api _api;

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

    [ObservableProperty]
    private string? _vehicleNote;

    [ObservableProperty]
    private bool _isAddChildMode;

    [ObservableProperty]
    private Guid? _activeDeclarationId;

    [ObservableProperty]
    private string _addChildBannerDeclarationLabel = string.Empty;

    public EntryViewModel(IB2Api api) => _api = api;

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
            var p = await _api.ListProcessorsAsync(includeInactive: false, page: 1, pageSize: 500).ConfigureAwait(false);
            var y = await _api.ListYardsAsync(includeInactive: false, page: 1, pageSize: 500).ConfigureAwait(false);
            Processors = p.Items;
            Yards = y.Items;
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
    private async Task SaveAsync() => await SubmitAsync(saveAndContinue: false).ConfigureAwait(false);

    [RelayCommand]
    private async Task SaveAndContinueAsync() => await SubmitAsync(saveAndContinue: true).ConfigureAwait(false);

    [RelayCommand]
    private void Cancel()
    {
        ErrorMessage = null;
        ResetEntireForm();
    }

    private async Task SubmitAsync(bool saveAndContinue)
    {
        ErrorMessage = null;
        var plate = PlateNumber.Trim().ToUpperInvariant();
        PlateNumber = plate;

        if (IsAddChildMode && ActiveDeclarationId is Guid existingId)
        {
            if (!Plate.IsMatch(plate))
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
                    .ConfigureAwait(false);
                ClearVehicleFields();
                await Toast.Make(AppResources.Entry_Toast_VehicleAdded).Show().ConfigureAwait(false);
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

        var decl = DeclarationNumber.Trim();
        var firstVehicle = new VehicleAddRequest(
            plate,
            NullIfWhiteSpace(TrailerOrContainerNumber),
            NullIfWhiteSpace(VehicleNote));

        var request = new DeclarationCreateRequest(
            decl,
            SelectedProcessor.Id,
            CompanyName.Trim(),
            GoodsName.Trim(),
            SelectedYard.Id,
            firstVehicle);

        try
        {
            var idem = Guid.NewGuid().ToString("N");
            var dto = await _api.CreateDeclarationAsync(request, idem).ConfigureAwait(false);
            if (saveAndContinue)
            {
                IsAddChildMode = true;
                ActiveDeclarationId = dto.Id;
                AddChildBannerDeclarationLabel = string.Format(CultureInfo.CurrentCulture, AddChildBannerFormat, MaskDeclaration(decl));
                ClearVehicleFields();
            }
            else
            {
                ResetEntireForm();
            }

            await Toast.Make(AppResources.Entry_Toast_Saved).Show().ConfigureAwait(false);
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.Entry_Error_CreateDeclaration;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Entry_Error_CreateDeclaration;
        }
    }

    private bool ValidateDeclarationForm(string plate, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        if (!DeclNumber.IsMatch(DeclarationNumber.Trim()))
        {
            error = AppResources.Entry_Error_DeclarationInvalid;
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

        if (!Plate.IsMatch(plate))
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
