using System.Globalization;
using B2.Desktop.Resources.Strings;
using B2.Shared.Contracts.Declarations;

namespace B2.Desktop.ViewModels;

public sealed class LedgerRowViewModel
{
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

    public string CompanyName => Dto.CompanyName;

    public string ProcessorName => Dto.ProcessorName;

    public string YardLabel => string.Format(CultureInfo.CurrentCulture, AppResources.Ledger_YardCode_Format, Dto.YardCode);

    public string PrimaryPlateDisplay =>
        string.IsNullOrWhiteSpace(Dto.PrimaryPlateNumber) ? AppResources.Common_EmDash : Dto.PrimaryPlateNumber;

    public string TrailerDisplay =>
        string.IsNullOrWhiteSpace(Dto.PrimaryTrailerOrContainer)
            ? AppResources.Common_EmDash
            : Dto.PrimaryTrailerOrContainer!;

    public string PlateAndTrailerLine =>
        string.IsNullOrWhiteSpace(Dto.PrimaryTrailerOrContainer)
            ? PrimaryPlateDisplay
            : string.Format(CultureInfo.CurrentCulture, AppResources.Ledger_PlateTrailer_Format, PrimaryPlateDisplay, Dto.PrimaryTrailerOrContainer);

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

        var masked = MaskDeclarationNumber(dto.DeclarationNumber);
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
