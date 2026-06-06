using FluentAssertions;

namespace B2.Desktop.Tests.Accessibility;

/// <summary>
/// T-S6-05 — WCAG 2.1 AA compliance tests, verbatim from spec.md UI Acceptance Criteria
/// (wireframe § 9, five scenarios). Implemented as static XAML analysis so they run
/// on ubuntu-latest without the MAUI workload.
/// </summary>
public sealed class AccessibilityTests
{
    private static string ViewsDir =>
        Path.Combine(AxeRunner.FindSolutionRoot(), "src", "B2.Desktop", "Views");

    private static string StylesDir =>
        Path.Combine(AxeRunner.FindSolutionRoot(), "src", "B2.Desktop", "Resources", "Styles");

    // -----------------------------------------------------------------------
    // Scenario: Keyboard-only navigation
    //   Given I am on any page
    //   When I press Tab repeatedly
    //   Then focus moves through all interactive elements in the tab order documented in § 6
    //   And the focus ring is visible at all times (Color.BorderFocus, 2px outline)
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "A11y")]
    public void Keyboard_navigation_all_interactive_elements_have_semantic_hints()
    {
        var violations = AxeRunner.FindInteractiveElementsMissingHints(ViewsDir);
        violations.Should().BeEmpty(
            because: "every interactive element (Button, Entry, Picker…) must carry " +
                     "SemanticProperties.Hint so screen readers can announce it and " +
                     "keyboard tab-order context is preserved (wireframe § 9 / Constitution VI)");
    }

    [Fact]
    [Trait("Category", "A11y")]
    public void Keyboard_navigation_focus_ring_token_exists_in_colors()
    {
        var tokens = AxeRunner.ParseColorTokens(Path.Combine(StylesDir, "Colors.xaml"));
        tokens.Should().ContainKey("ColorBorderFocus",
            because: "wireframe § 9 mandates a 2px ColorBorderFocus outline on every focused " +
                     "interactive control");

        tokens["ColorBorderFocus"].Should().Be("#1A73E8",
            because: "the focus-ring colour must exactly match the wireframe § 9 spec value");
    }

    // -----------------------------------------------------------------------
    // Scenario: Screen reader announces state changes
    //   Given I am on the Ledger
    //   When a vehicle exit is pushed via SignalR
    //   Then the row's status badge update is announced via aria-live="polite"
    //   And nothing is announced if the user is in a text input
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "A11y")]
    public void Screen_reader_ledger_status_labels_have_semantic_descriptions()
    {
        var hasDescriptions = AxeRunner.LedgerPageStatusLabelsHaveDescriptions(ViewsDir);
        hasDescriptions.Should().BeTrue(
            because: "Ledger status-change labels must carry SemanticProperties.Description " +
                     "so screen readers announce live state changes (WCAG 4.1.3 / wireframe § 9)");
    }

    // -----------------------------------------------------------------------
    // Scenario: Color is never sole indicator
    //   Given any status (Open / AllExited / KVGSConfirmed)
    //   Then the row has BOTH a colored background AND an icon AND a text label
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "A11y")]
    public void Color_not_sole_indicator_ledger_status_rows_show_icon_and_label()
    {
        var hasTriple = AxeRunner.LedgerPageHasStatusTriple(ViewsDir);
        hasTriple.Should().BeTrue(
            because: "WCAG 1.4.1 forbids using colour as the only visual means of conveying " +
                     "information — each status row must show icon (StatusIcon) + text " +
                     "(StatusLabel) + colour trigger (ColorLedgerStatus*)");
    }

    // -----------------------------------------------------------------------
    // Scenario: Large text mode
    //   Given I select "Cỡ chữ: Lớn"
    //   Then all font sizes increase by 4 (e.g., body 16 → 20)
    //   And no text is clipped, no layout breaks
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "A11y")]
    public void Large_text_mode_typography_tokens_have_correct_base_values()
    {
        var violations = AxeRunner.FindFontTokenViolations(StylesDir);
        violations.Should().BeEmpty(
            because: "App.xaml.cs applies a +4/+8 offset at startup by adding to the base " +
                     "values in Typography.xaml; if the bases are wrong the Large/ExtraLarge " +
                     "modes silently produce incorrect sizes (wireframe § 3 / § 9)");
    }

    // -----------------------------------------------------------------------
    // Scenario: Contrast ratios
    //   Then every text/background pair in the rendered UI meets ≥ 4.5:1 (normal)
    //   or ≥ 3:1 (large/UI)
    //   Verified by automated audit in CI (axe-style for MAUI)
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "A11y")]
    public void Contrast_ratios_text_background_pairs_meet_wcag_aa()
    {
        var violations = AxeRunner.FindContrastViolations(StylesDir);
        violations.Should().BeEmpty(
            because: "WCAG 1.4.3 requires ≥ 4.5:1 for normal text and ≥ 3:1 for large/UI " +
                     "text — all token pairs in Colors.xaml must pass (wireframe § 2)");
    }
}
