using System.Globalization;
using System.Text;
using B2.Desktop.Resources.Strings;
using B2.Shared.Contracts.Declarations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace B2.Desktop.ViewModels;

public sealed partial class LedgerRowViewModel : ObservableObject
{
    private static readonly CompositeFormat YardCodeFormat = CompositeFormat.Parse(AppResources.Ledger_YardCode_Format);
    private static readonly CompositeFormat MoreNumbersFormat = CompositeFormat.Parse(AppResources.Ledger_MoreNumbers_Format);

    /// <summary>
    /// Bound by KvgsPage's row template; used by <c>KvgsViewModel</c> to fade
    /// the row out over 300 ms after a successful confirmation per
    /// wireframe § 6.5. Default 1.0 means fully visible.
    /// </summary>
    [ObservableProperty]
    private double _opacity = 1.0;

    private LedgerRowViewModel(
        DeclarationSummaryDto dto,
        int rowOrdinal,
        string statusIcon,
        string statusLabel,
        string declarationMasked,
        string timeLocal)
    {
        Dto = dto;
        RowOrdinal = rowOrdinal;
        StatusIcon = statusIcon;
        StatusLabel = statusLabel;
        DeclarationMasked = declarationMasked;
        TimeLocal = timeLocal;
    }

    public DeclarationSummaryDto Dto { get; }

    public int RowOrdinal { get; }

    public string StatusIcon { get; }

    public string StatusLabel { get; }

    public string DeclarationMasked { get; }

    public string TimeLocal { get; }

    /// <summary>FR-023: cancelled rows render struck-through in the ledger.</summary>
    public bool IsCancelled => string.Equals(Dto.Status, "Cancelled", StringComparison.Ordinal);

    public string CompanyName => Dto.CompanyName;

    public string ProcessorName => Dto.ProcessorName;

    public string YardLabel => string.Format(CultureInfo.CurrentCulture, YardCodeFormat, Dto.YardCode);

    public string PrimaryPlateDisplay =>
        string.IsNullOrWhiteSpace(Dto.PrimaryPlateNumber) ? AppResources.Common_EmDash : Dto.PrimaryPlateNumber;

    public string TrailerDisplay =>
        string.IsNullOrWhiteSpace(Dto.PrimaryTrailerOrContainer)
            ? AppResources.Common_EmDash
            : Dto.PrimaryTrailerOrContainer!;

    public static LedgerRowViewModel FromDto(DeclarationSummaryDto dto, int rowOrdinal)
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var (icon, label) = dto.Status switch
        {
            "Open" => ("🔴", AppResources.Ledger_Status_Open),
            "AllExited" => ("🟢", AppResources.Ledger_Status_AllExited),
            "KVGSConfirmed" => ("🔵", AppResources.Ledger_Status_KvgsConfirmed),
            "Cancelled" => ("⛔", AppResources.Ledger_Status_Cancelled),
            _ => ("❔", dto.Status),
        };

        // A row may carry several tờ khai (cột C); the ledger shows the primary plus a count so
        // the officer can see there is more without opening the drawer.
        var masked = MaskDeclarationNumber(dto.DeclarationNumber);
        if (dto.DeclarationNumberCount > 1)
        {
            masked = masked + " " + string.Format(
                culture,
                MoreNumbersFormat,
                dto.DeclarationNumberCount - 1);
        }

        var time = dto.EnteredAt.ToLocalTime().ToString("HH:mm", culture);
        return new LedgerRowViewModel(dto, rowOrdinal, icon, label, masked, time);
    }

    private static string MaskDeclarationNumber(string n)
    {
        if (n.Length != 12)
        {
            return n;
        }

        return string.Concat(n.AsSpan(0, 4), "...", n.AsSpan(10, 2));
    }
}
