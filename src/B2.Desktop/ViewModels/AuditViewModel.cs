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
/// Wireframe § 6.12 — Nhật ký kiểm toán (F-16). Read-only, filterable, paginated view
/// of the immutable audit log; a selected row reveals its before/after JSON. No mutation
/// surface exists (the audit log is append-only, FR-003).
/// </summary>
public sealed partial class AuditViewModel : ObservableObject
{
    private static readonly CompositeFormat PageSummaryFormat = CompositeFormat.Parse(AppResources.Audit_PageSummary_Format);
    private static readonly CompositeFormat ErrorFormat = CompositeFormat.Parse(AppResources.Audit_Error_Format);

    private const int PageSizeValue = 50;

    private readonly IB2Api _api;

    public AuditViewModel(IB2Api api)
    {
        _api = api;
        var today = DateTime.Today;
        _fromDate = today.AddDays(-7);
        _toDate = today;
    }

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    [ObservableProperty]
    private int _entityTypeIndex;

    [ObservableProperty]
    private int _actionIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private AuditLogItemDto? _selectedItem;

    public ObservableCollection<AuditLogItemDto> Items { get; } = new();

    // Index 0 is "all" (no filter); the rest are the entity types that emit audit rows.
    public IReadOnlyList<string> EntityTypeOptions { get; } = new[]
    {
        AppResources.Audit_Filter_All, "Declaration", "Vehicle", "User", "Processor", "Yard", "DailyHeader",
    };

    public IReadOnlyList<string> ActionOptions { get; } = new[]
    {
        AppResources.Audit_Filter_All, "Create", "Update", "Delete", "Cancel", "OfficerNameChanged",
    };

    public string PageSummary => string.Format(CultureInfo.CurrentCulture, PageSummaryFormat, Items.Count, Total);

    public bool HasNoData => !IsLoading && string.IsNullOrEmpty(ErrorMessage) && Items.Count == 0;

    public bool HasDetail => SelectedItem is not null;

    partial void OnSelectedItemChanged(AuditLogItemDto? value) => OnPropertyChanged(nameof(HasDetail));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasNoData));
        try
        {
            var entityType = EntityTypeIndex > 0 ? EntityTypeOptions[EntityTypeIndex] : null;
            var action = ActionIndex > 0 ? ActionOptions[ActionIndex] : null;
            var from = new DateTimeOffset(FromDate.Date).ToString("o", CultureInfo.InvariantCulture);
            var to = new DateTimeOffset(ToDate.Date.AddDays(1).AddTicks(-1)).ToString("o", CultureInfo.InvariantCulture);

            var response = await _api.ListAuditAsync(entityType, action, from, to, Page, PageSizeValue).ConfigureAwait(true);
            Items.Clear();
            foreach (var item in response.Items)
            {
                Items.Add(item);
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
            ErrorMessage = AppResources.Audit_Error_Network;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoData));
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        Page = 1;
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page * PageSizeValue >= Total)
        {
            return;
        }

        Page++;
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (Page <= 1)
        {
            return;
        }

        Page--;
        await LoadAsync().ConfigureAwait(true);
    }
}
