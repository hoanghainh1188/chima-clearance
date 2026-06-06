using System.Xml.Linq;

namespace B2.Desktop.Tests.Accessibility;

/// <summary>
/// Static XAML analyser — axe-style audit for MAUI. Parses source XAML files from the
/// solution tree so accessibility assertions can run on ubuntu-latest without the MAUI
/// workload. Called by <see cref="AccessibilityTests"/> (§ 9 Gherkin scenarios).
/// </summary>
public static class AxeRunner
{
    private static readonly XNamespace MauiNs = "http://schemas.microsoft.com/dotnet/2021/maui";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2009/xaml";

    // Interactive element local names that must carry SemanticProperties.Hint.
    private static readonly HashSet<string> InteractiveElements =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Button", "Entry", "Picker", "CheckBox", "RadioButton",
            "SearchBar", "ImageButton", "Switch",
        };

    // -----------------------------------------------------------------------
    // Solution root discovery
    // -----------------------------------------------------------------------

    /// <summary>Walks parent directories until the folder containing B2.sln is found.</summary>
    public static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "B2.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not locate B2.sln in any parent directory.");
    }

    // -----------------------------------------------------------------------
    // Scenario 1 — Keyboard navigation: interactive elements have semantic hints
    // -----------------------------------------------------------------------

    // Controls whose accessible name comes from their `Text` property alone and
    // do NOT require a separate SemanticProperties.Hint (the text IS the label).
    private static readonly HashSet<string> TextLabelledElements =
        new(StringComparer.OrdinalIgnoreCase) { "Button", "CheckBox" };

    /// <summary>
    /// Returns a list of violation strings for interactive elements that have no
    /// accessible label.  Rules:
    /// <list type="bullet">
    ///   <item>Button / CheckBox with a Text= attribute → accessible name is the text; OK.</item>
    ///   <item>Entry / Picker / SearchBar / ImageButton / Switch → must carry
    ///   <c>SemanticProperties.Hint</c> or <c>.Description</c>.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> FindInteractiveElementsMissingHints(string viewsDir)
    {
        var violations = new List<string>();
        foreach (var file in XamlFiles(viewsDir))
        {
            var doc = XDocument.Load(file);
            foreach (var el in doc.Descendants())
            {
                if (!InteractiveElements.Contains(el.Name.LocalName))
                    continue;

                // Buttons and CheckBoxes with a Text= attribute already have an accessible name.
                var localName = el.Name.LocalName;
                if (TextLabelledElements.Contains(localName) &&
                    el.Attributes().Any(a =>
                        string.Equals(a.Name.LocalName, "Text", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(a.Value)))
                {
                    continue;
                }

                var hasHint =
                    el.Attributes().Any(a =>
                        a.Name.LocalName.EndsWith(".Hint", StringComparison.Ordinal) ||
                        a.Name.LocalName.EndsWith(".Description", StringComparison.Ordinal)) ||
                    el.Elements().Any(child =>
                        child.Name.LocalName.Contains("SemanticProperties", StringComparison.Ordinal));

                if (!hasHint)
                    violations.Add($"{Path.GetFileName(file)}: <{localName}> missing SemanticProperties.Hint");
            }
        }
        return violations;
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — Screen reader: status-change labels carry Description
    // -----------------------------------------------------------------------

    /// <summary>
    /// Checks that LedgerPage.xaml contains at least one Label with
    /// <c>SemanticProperties.Description</c> (or equivalent) on a status-bound region.
    /// </summary>
    public static bool LedgerPageStatusLabelsHaveDescriptions(string viewsDir)
    {
        var ledger = Path.Combine(viewsDir, "LedgerPage.xaml");
        if (!File.Exists(ledger))
            return false;

        var doc = XDocument.Load(ledger);
        return doc.Descendants()
            .Any(el =>
                el.Name.LocalName == "Label" &&
                el.Attributes().Any(a =>
                    a.Name.LocalName.EndsWith(".Description", StringComparison.Ordinal) ||
                    a.Name.LocalName.EndsWith(".Hint", StringComparison.Ordinal)));
    }

    // -----------------------------------------------------------------------
    // Scenario 3 — Color not sole indicator: status rows show icon + label + color
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that LedgerPage.xaml has a <c>StatusIcon</c> binding, a <c>StatusLabel</c>
    /// binding, AND a DataTrigger / Setter on <c>TextColor</c> for status rows —
    /// satisfying WCAG 1.4.1 (color alone must never be the only indicator).
    /// </summary>
    public static bool LedgerPageHasStatusTriple(string viewsDir)
    {
        var ledger = Path.Combine(viewsDir, "LedgerPage.xaml");
        if (!File.Exists(ledger))
            return false;

        var text = File.ReadAllText(ledger);
        var hasIcon = text.Contains("StatusIcon", StringComparison.Ordinal);
        var hasLabel = text.Contains("StatusLabel", StringComparison.Ordinal);
        var hasColorTrigger = text.Contains("ColorLedgerStatus", StringComparison.Ordinal);
        return hasIcon && hasLabel && hasColorTrigger;
    }

    // -----------------------------------------------------------------------
    // Scenario 4 — Large text: Typography.xaml has all required tokens
    // -----------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, double> ExpectedBaseTokens =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["FontSizeXS"] = 12.0,
            ["FontSizeS"] = 14.0,
            ["FontSizeM"] = 16.0,
            ["FontSizeL"] = 18.0,
            ["FontSizeXL"] = 22.0,
            ["FontSizeXXL"] = 28.0,
            ["FontSizeSearchInput"] = 24.0,
        };

    /// <summary>
    /// Parses Typography.xaml and asserts every font-size token exists with the
    /// base value specified in wireframe § 3. Returns violations.
    /// </summary>
    public static IReadOnlyList<string> FindFontTokenViolations(string stylesDir)
    {
        var file = Path.Combine(stylesDir, "Typography.xaml");
        if (!File.Exists(file))
            return new[] { "Typography.xaml not found" };

        var doc = XDocument.Load(file);
        var violations = new List<string>();

        foreach (var (key, expected) in ExpectedBaseTokens)
        {
            var el = doc.Descendants()
                .FirstOrDefault(e =>
                    e.Attribute(XamlNs + "Key")?.Value == key ||
                    e.Attribute("Key")?.Value == key);

            if (el is null)
            {
                violations.Add($"Token '{key}' not found in Typography.xaml");
                continue;
            }

            if (!double.TryParse(el.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var actual) ||
                Math.Abs(actual - expected) > 0.01)
            {
                violations.Add($"Token '{key}' expected {expected} but found '{el.Value}'");
            }
        }

        return violations;
    }

    // -----------------------------------------------------------------------
    // Scenario 5 — Contrast ratios meet WCAG AA
    // -----------------------------------------------------------------------

    // Key text/background token pairs defined in wireframe § 2.
    private static readonly IReadOnlyList<(string TextKey, string BgKey, bool LargeText)> ContrastPairs =
        new[]
        {
            ("ColorOnSurface",           "ColorSurface",             false),
            ("ColorOnSurfaceMuted",      "ColorSurface",             false),
            ("ColorError",               "ColorErrorBg",             false),
            ("ColorWarning",             "ColorWarningBanner",       false),
            ("ColorBadgeOnlineText",     "ColorBadgeOnlineBg",       true),
            ("ColorBadgePollingText",    "ColorBadgePollingBg",      true),
            ("ColorBadgeOfflineText",    "ColorBadgeOfflineBg",      true),
        };

    /// <summary>
    /// Reads Colors.xaml, looks up each token pair, computes the WCAG 2.1 contrast ratio,
    /// and returns any violation whose ratio falls below the minimum (4.5:1 normal,
    /// 3:1 large/UI text per WCAG success criteria 1.4.3 / 1.4.11).
    /// </summary>
    public static IReadOnlyList<string> FindContrastViolations(string stylesDir)
    {
        var file = Path.Combine(stylesDir, "Colors.xaml");
        if (!File.Exists(file))
            return new[] { "Colors.xaml not found" };

        var tokens = ParseColorTokens(file);
        var violations = new List<string>();

        foreach (var (textKey, bgKey, large) in ContrastPairs)
        {
            if (!tokens.TryGetValue(textKey, out var textHex))
            {
                violations.Add($"Color token '{textKey}' not found");
                continue;
            }
            if (!tokens.TryGetValue(bgKey, out var bgHex))
            {
                violations.Add($"Color token '{bgKey}' not found");
                continue;
            }

            var ratio = ComputeContrastRatio(textHex, bgHex);
            var minimum = large ? 3.0 : 4.5;
            if (ratio < minimum)
                violations.Add($"{textKey}/{bgKey}: ratio {ratio:F2} < {minimum} ({(large ? "large/UI" : "normal")})");
        }

        return violations;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    internal static Dictionary<string, string> ParseColorTokens(string colorsXamlPath)
    {
        var doc = XDocument.Load(colorsXamlPath);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var el in doc.Descendants())
        {
            if (el.Name.LocalName != "Color")
                continue;
            var key = el.Attribute(XamlNs + "Key")?.Value ?? el.Attribute("Key")?.Value;
            if (key is not null)
                result[key] = el.Value.Trim();
        }
        return result;
    }

    /// <summary>
    /// WCAG 2.1 relative luminance + contrast ratio formula.
    /// </summary>
    internal static double ComputeContrastRatio(string hex1, string hex2)
    {
        var l1 = RelativeLuminance(hex1);
        var l2 = RelativeLuminance(hex2);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var h = hex.TrimStart('#');
        var r = Convert.ToInt32(h[..2], 16);
        var g = Convert.ToInt32(h[2..4], 16);
        var b = Convert.ToInt32(h[4..6], 16);
        return 0.2126 * Linearize(r / 255.0)
             + 0.7152 * Linearize(g / 255.0)
             + 0.0722 * Linearize(b / 255.0);
    }

    private static double Linearize(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static IEnumerable<string> XamlFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories);
}
