using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.Declarations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

/// <summary>
/// One vehicle row inside the ledger detail drawer. The drawer used to bind the immutable
/// <see cref="VehicleDetailDto"/> directly; "Ghi chú" (cột O) is editable at the exit gate per
/// business rules § 3, so each row needs its own editable state and save command.
/// </summary>
public sealed partial class DrawerVehicleViewModel : ObservableObject
{
    private readonly IB2Api _api;
    private readonly IToastService _toast;
    private string _savedNote;

    [ObservableProperty]
    private string _note;

    [ObservableProperty]
    private bool _isSaving;

    public DrawerVehicleViewModel(VehicleDetailDto dto, IB2Api api, IToastService toast)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _api = api;
        _toast = toast;
        Dto = dto;
        _savedNote = dto.Note ?? string.Empty;
        _note = _savedNote;
    }

    public VehicleDetailDto Dto { get; }

    public Guid Id => Dto.Id;

    public string PlateNumber => Dto.PlateNumber;

    public string? TrailerOrContainerNumber => Dto.TrailerOrContainerNumber;

    public DateTimeOffset EntryTime => Dto.EntryTime;

    public bool ExitMarked => Dto.ExitMarked;

    /// <summary>Nothing to save until the text actually differs from what the server holds.</summary>
    public bool CanSaveNote => !IsSaving && !string.Equals(Note, _savedNote, StringComparison.Ordinal);

    [RelayCommand(CanExecute = nameof(CanSaveNote))]
    private async Task SaveNoteAsync()
    {
        IsSaving = true;
        try
        {
            await _api.UpdateVehicleNoteAsync(Id, new UpdateVehicleNoteRequest(Note)).ConfigureAwait(true);
            _savedNote = Note;
            await _toast.ShowAsync(AppResources.Ledger_Drawer_NoteSaved, ToastSeverity.Success).ConfigureAwait(true);
        }
        catch (ApiException)
        {
            await _toast.ShowAsync(AppResources.Ledger_Drawer_NoteSaveError, ToastSeverity.Error).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await _toast.ShowAsync(AppResources.Ledger_Drawer_NoteSaveError, ToastSeverity.Error).ConfigureAwait(true);
        }
        finally
        {
            IsSaving = false;
            OnPropertyChanged(nameof(CanSaveNote));
            SaveNoteCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnNoteChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveNote));
        SaveNoteCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveNote));
        SaveNoteCommand.NotifyCanExecuteChanged();
    }
}
