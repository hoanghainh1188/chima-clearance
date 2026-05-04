using B2.Desktop.Resources.Strings;
using B2.Desktop.Services.Api;
using B2.Shared.Contracts.MasterData;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace B2.Desktop.ViewModels.Admin;

public partial class MasterDataViewModel : ObservableObject
{
    private readonly IB2Api _api;

    [ObservableProperty]
    private bool _isProcessorsTab = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private IReadOnlyList<ProcessorDto> _processors = Array.Empty<ProcessorDto>();

    [ObservableProperty]
    private IReadOnlyList<YardDto> _yards = Array.Empty<YardDto>();

    public MasterDataViewModel(IB2Api api) => _api = api;

    [RelayCommand]
    private void SelectProcessorsTab() => IsProcessorsTab = true;

    [RelayCommand]
    private void SelectYardsTab() => IsProcessorsTab = false;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var p = await _api.ListProcessorsAsync(includeInactive: true, page: 1, pageSize: 500).ConfigureAwait(false);
            var y = await _api.ListYardsAsync(includeInactive: true, page: 1, pageSize: 500).ConfigureAwait(false);
            Processors = p.Items;
            Yards = y.Items;
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.MasterData_Error_Load;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddProcessorAsync()
    {
        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return;
        }

        var name = await page.DisplayPromptAsync(
            AppResources.MasterData_AddProcessorTitle,
            AppResources.MasterData_ProcessorNamePrompt,
            initialValue: string.Empty,
            maxLength: 200).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.CreateProcessorAsync(new ProcessorWriteRequest(name.Trim(), null)).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
    }

    [RelayCommand]
    private async Task EditProcessorAsync(ProcessorDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return;
        }

        var name = await page.DisplayPromptAsync(
            AppResources.MasterData_EditProcessorTitle,
            AppResources.MasterData_ProcessorNamePrompt,
            initialValue: dto.FullName,
            maxLength: 200).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.UpdateProcessorAsync(dto.Id, new ProcessorWriteRequest(name.Trim(), null)).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
    }

    [RelayCommand]
    private async Task ToggleProcessorActiveAsync(ProcessorDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return;
        }

        var activate = !dto.IsActive;
        if (!activate)
        {
            var confirm = await page.DisplayAlert(
                AppResources.MasterData_DeactivateTitle,
                AppResources.MasterData_DeactivateProcessorMessage,
                AppResources.Common_Yes,
                AppResources.Common_No).ConfigureAwait(false);
            if (!confirm)
            {
                return;
            }
        }

        try
        {
            if (activate)
            {
                await _api.UpdateProcessorAsync(dto.Id, new ProcessorWriteRequest(dto.FullName, true)).ConfigureAwait(false);
            }
            else
            {
                await _api.DeleteProcessorAsync(dto.Id).ConfigureAwait(false);
            }

            await LoadAsync().ConfigureAwait(false);
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
    }

    [RelayCommand]
    private async Task AddYardAsync()
    {
        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return;
        }

        var code = await page.DisplayPromptAsync(
            AppResources.MasterData_AddYardTitle,
            AppResources.MasterData_YardCodePrompt,
            initialValue: string.Empty,
            maxLength: 32).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var name = await page.DisplayPromptAsync(
            AppResources.MasterData_AddYardTitle,
            AppResources.MasterData_YardNamePrompt,
            initialValue: string.Empty,
            maxLength: 200).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.CreateYardAsync(new YardWriteRequest(code.Trim(), name.Trim(), null)).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
    }

    [RelayCommand]
    private async Task EditYardAsync(YardDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return;
        }

        var code = await page.DisplayPromptAsync(
            AppResources.MasterData_EditYardTitle,
            AppResources.MasterData_YardCodePrompt,
            initialValue: dto.Code,
            maxLength: 32).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var name = await page.DisplayPromptAsync(
            AppResources.MasterData_EditYardTitle,
            AppResources.MasterData_YardNamePrompt,
            initialValue: dto.Name,
            maxLength: 200).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.UpdateYardAsync(dto.Id, new YardWriteRequest(code.Trim(), name.Trim(), null)).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
    }

    [RelayCommand]
    private async Task ToggleYardActiveAsync(YardDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return;
        }

        var activate = !dto.IsActive;
        if (!activate)
        {
            var confirm = await page.DisplayAlert(
                AppResources.MasterData_DeactivateTitle,
                AppResources.MasterData_DeactivateYardMessage,
                AppResources.Common_Yes,
                AppResources.Common_No).ConfigureAwait(false);
            if (!confirm)
            {
                return;
            }
        }

        try
        {
            if (activate)
            {
                await _api.UpdateYardAsync(dto.Id, new YardWriteRequest(dto.Code, dto.Name, true)).ConfigureAwait(false);
            }
            else
            {
                await _api.DeleteYardAsync(dto.Id).ConfigureAwait(false);
            }

            await LoadAsync().ConfigureAwait(false);
        }
        catch (ApiException)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.MasterData_Error_Save;
        }
    }
}
