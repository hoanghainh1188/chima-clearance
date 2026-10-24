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
/// Snapshots are streamed through a temp file / straight from disk, never held in memory
/// (ADR-0011): a few-hundred-MB database must not cost the gate PC that much RAM.
/// </summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private const string ConfirmWord = "RESTORE";
    private const int FileBufferBytes = 81_920;

    private static readonly string[] BakExtensions = { ".bak" };

    private static readonly CompositeFormat BackupSuccessFormat = CompositeFormat.Parse(AppResources.Backup_Toast_BackupSuccess_Format);
    private static readonly CompositeFormat LastBackupFormat = CompositeFormat.Parse(AppResources.Backup_LastBackup_Format);
    private static readonly CompositeFormat SelectedFileFormat = CompositeFormat.Parse(AppResources.Backup_Restore_SelectedFile_Format);
    private static readonly CompositeFormat ErrorFormat = CompositeFormat.Parse(AppResources.Backup_Error_Format);

    private readonly IB2Api _api;
    private readonly IToastService _toast;

    private FileResult? _selectedFile;

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
        && _selectedFile is not null
        && string.Equals(RestoreConfirmText, ConfirmWord, StringComparison.Ordinal);

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

            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "b2-backup.bak";

            // Download to a self-deleting temp file first: the Save-As dialog can stay open for
            // a while, and the server would drop a response the client stops reading.
            using var download = CreateTempFile();
            using (var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(true))
            {
                await body.CopyToAsync(download).ConfigureAwait(true);
            }

            download.Position = 0;
            var result = await FileSaver.Default.SaveAsync(fileName, download, CancellationToken.None).ConfigureAwait(true);
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

        _selectedFile = picked;

        SelectedFileLabel = string.Format(CultureInfo.CurrentCulture, SelectedFileFormat, picked.FileName);
        RestoreConfirmText = string.Empty;
        RestorePassword = string.Empty;
        IsRestoreConfirmVisible = true;
        RestoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        if (IsBusy || _selectedFile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            using var file = await _selectedFile.OpenReadAsync().ConfigureAwait(true);
            var part = new StreamPart(file, _selectedFile.FileName, "application/octet-stream");
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
        _selectedFile = null;
        SelectedFileLabel = null;
        RestoreConfirmText = string.Empty;
        RestorePassword = string.Empty;
        IsRestoreConfirmVisible = false;
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private static FileStream CreateTempFile() =>
        new(
            Path.Combine(Path.GetTempPath(), $"b2-download-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            FileBufferBytes,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

    private async Task ShowErrorAsync(int statusCode) =>
        await _toast.ShowAsync(string.Format(CultureInfo.CurrentCulture, ErrorFormat, statusCode), ToastSeverity.Error).ConfigureAwait(true);
}
