using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels;

/// <summary>
/// Wireframe § 6.10 — Sao lưu & khôi phục (F-15, Admin). On-demand encrypted snapshot
/// (download via Save As) and restore (pick a .bak → typed-"RESTORE" confirmation +
/// backup secret → upload). Continuous (Litestream) status is a placeholder until T-S5-08.
/// </summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private const string ConfirmWord = "RESTORE";

    private static readonly string[] BakExtensions = { ".bak" };

    private static readonly CompositeFormat BackupSuccessFormat = CompositeFormat.Parse(AppResources.Backup_Toast_BackupSuccess_Format);
    private static readonly CompositeFormat LastBackupFormat = CompositeFormat.Parse(AppResources.Backup_LastBackup_Format);
    private static readonly CompositeFormat SelectedFileFormat = CompositeFormat.Parse(AppResources.Backup_Restore_SelectedFile_Format);
    private static readonly CompositeFormat ErrorFormat = CompositeFormat.Parse(AppResources.Backup_Error_Format);

    private readonly IB2Api _api;
    private readonly IToastService _toast;

    private byte[]? _selectedFileBytes;
    private string? _selectedFileName;

    public BackupViewModel(IB2Api api, IToastService toast)
    {
        _api = api;
        _toast = toast;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _lastBackupText;

    [ObservableProperty]
    private string? _selectedFileLabel;

    [ObservableProperty]
    private bool _isRestoreConfirmVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private string _restoreConfirmText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private string _restorePassword = string.Empty;

    private bool CanRestore =>
        IsRestoreConfirmVisible
        && _selectedFileBytes is not null
        && string.Equals(RestoreConfirmText, ConfirmWord, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(RestorePassword);

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            using var response = await _api.BackupNowAsync().ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                await ShowErrorAsync((int)response.StatusCode).ConfigureAwait(true);
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(true);
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "b2-backup.bak";

            using var stream = new MemoryStream(bytes);
            var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None).ConfigureAwait(true);
            if (!result.IsSuccessful)
            {
                await _toast.ShowAsync(AppResources.Backup_Toast_BackupCancelled, ToastSeverity.Warning).ConfigureAwait(true);
                return;
            }

            LastBackupText = string.Format(CultureInfo.CurrentCulture, LastBackupFormat, fileName);
            await _toast.ShowAsync(string.Format(CultureInfo.CurrentCulture, BackupSuccessFormat, fileName), ToastSeverity.Success).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await _toast.ShowAsync(AppResources.Backup_Error_Network, ToastSeverity.Error).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectFileAsync()
    {
        var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = BakExtensions,
        });

        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = AppResources.Backup_Restore_SelectFile,
            FileTypes = fileType,
        }).ConfigureAwait(true);

        if (picked is null)
        {
            return;
        }

        using var stream = await picked.OpenReadAsync().ConfigureAwait(true);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(true);
        _selectedFileBytes = buffer.ToArray();
        _selectedFileName = picked.FileName;

        SelectedFileLabel = string.Format(CultureInfo.CurrentCulture, SelectedFileFormat, picked.FileName);
        RestoreConfirmText = string.Empty;
        RestorePassword = string.Empty;
        IsRestoreConfirmVisible = true;
        RestoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        if (IsBusy || _selectedFileBytes is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var part = new ByteArrayPart(_selectedFileBytes, _selectedFileName ?? "snapshot.bak", "application/octet-stream");
            using var response = await _api.RestoreAsync(part, RestorePassword).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                await ShowErrorAsync((int)response.StatusCode).ConfigureAwait(true);
                return;
            }

            CancelRestore();
            await _toast.ShowAsync(AppResources.Backup_Toast_RestoreRequested, ToastSeverity.Success).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await _toast.ShowAsync(AppResources.Backup_Error_Network, ToastSeverity.Error).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelRestore()
    {
        _selectedFileBytes = null;
        _selectedFileName = null;
        SelectedFileLabel = null;
        RestoreConfirmText = string.Empty;
        RestorePassword = string.Empty;
        IsRestoreConfirmVisible = false;
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private async Task ShowErrorAsync(int statusCode) =>
        await _toast.ShowAsync(string.Format(CultureInfo.CurrentCulture, ErrorFormat, statusCode), ToastSeverity.Error).ConfigureAwait(true);
}
