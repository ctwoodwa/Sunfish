using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Sunfish.Anchor.Tests.A11y;

/// <summary>
/// W#48 Phase 4 — WCAG 2.2 AA + EN 301 549 v3.2.1 structural a11y assertions for
/// the Atlas Integration-Config UI surface (ADR 0067).
///
/// Verified via source-file structural inspection per the Phase 3 precedent (W#47).
/// Components under test:
///   AtlasIntegrationConfig.razor    — WAI-ARIA APG Tabs pattern, roving tabindex
///   AtlasIntegrationCategoryPanel.razor — SC 4.1.2 aria-disabled, SC 4.1.3 live region
///   AtlasCredentialField.razor      — SC 3.3.2 aria-describedby, SC 3.3.7 leave-unchanged,
///                                     SC 3.3.8 autocomplete, SC 4.1.2 aria-pressed+aria-controls
/// </summary>
public sealed class AtlasIntegrationConfigA11yTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // AtlasIntegrationConfig.razor — WAI-ARIA APG Tabs pattern
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IntegrationConfig_TablistHasAriaLabel()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        Assert.True(
            markup.Contains("role=\"tablist\""),
            "AtlasIntegrationConfig.razor must declare role=\"tablist\" (APG Tabs pattern).");

        Assert.True(
            markup.Contains("aria-label=\"Integration categories\""),
            "AtlasIntegrationConfig.razor must declare aria-label=\"Integration categories\" " +
            "on the tablist element (SC 4.1.2 — accessible name for the tab group).");
    }

    [Fact]
    public void IntegrationConfig_TabsHaveRoleAndAriaSelected()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        Assert.True(
            markup.Contains("role=\"tab\""),
            "AtlasIntegrationConfig.razor must declare role=\"tab\" on tab buttons (APG Tabs).");

        Assert.True(
            markup.Contains("aria-selected="),
            "AtlasIntegrationConfig.razor must bind aria-selected on tab buttons (APG Tabs).");
    }

    [Fact]
    public void IntegrationConfig_TabsHaveRovingTabindex()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        // Roving tabindex: active tab has tabindex="0", others tabindex="-1".
        // Razor typically emits tabindex as a bound expression like tabindex="@(isActive ? "0" : "-1")",
        // so we verify both literal values appear somewhere in the source.
        Assert.True(
            markup.Contains("tabindex") && markup.Contains("\"0\"") && markup.Contains("\"-1\""),
            "AtlasIntegrationConfig.razor must implement roving tabindex: active tab " +
            "tabindex=\"0\", inactive tabs tabindex=\"-1\" (APG Tabs §3.5 keyboard interaction).");
    }

    [Fact]
    public void IntegrationConfig_KeyboardNavigationHandlerPresent()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        Assert.True(
            markup.Contains("ArrowLeft") && markup.Contains("ArrowRight"),
            "AtlasIntegrationConfig.razor must handle ArrowLeft + ArrowRight keys for " +
            "tab navigation per WAI-ARIA APG Tabs keyboard interaction.");

        Assert.True(
            markup.Contains("Home") && markup.Contains("End"),
            "AtlasIntegrationConfig.razor must handle Home + End keys for tab navigation " +
            "per WAI-ARIA APG Tabs keyboard interaction.");
    }

    [Fact]
    public void IntegrationConfig_FocusAsyncCalledOnKeyNavigation()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        Assert.True(
            markup.Contains("FocusAsync"),
            "AtlasIntegrationConfig.razor must call FocusAsync() when navigating tabs " +
            "via keyboard (APG Tabs — focus must follow active tab on ArrowLeft/Right).");
    }

    [Fact]
    public void IntegrationConfig_InactivePanelsHaveHiddenAttribute()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        // Inactive panels must use hidden (not CSS display:none) for AT compat
        Assert.True(
            markup.Contains("hidden"),
            "AtlasIntegrationConfig.razor must use the 'hidden' attribute on inactive " +
            "tabpanels (APG Tabs §3.6 — hidden attribute preferred over CSS for AT compat).");
    }

    [Fact]
    public void IntegrationConfig_PanelsHaveAriaLabelledby()
    {
        var markup = ReadSource("AtlasIntegrationConfig.razor");

        Assert.True(
            markup.Contains("aria-labelledby="),
            "AtlasIntegrationConfig.razor must declare aria-labelledby on tabpanel elements " +
            "pointing to the corresponding tab id (APG Tabs — SC 1.3.1).");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AtlasIntegrationCategoryPanel.razor — validate button + status live region
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CategoryPanel_ValidateButtonUsesAriaDisabledNotNativeDisabled()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        // SC 4.1.2: aria-disabled keeps button focusable during validation;
        // native 'disabled' removes it from the tab order (confusing to AT users).
        Assert.True(
            markup.Contains("aria-disabled="),
            "AtlasIntegrationCategoryPanel.razor must use aria-disabled on the validate button " +
            "during validation (SC 4.1.2 — keeps button focusable per WCAG advisory technique).");
    }

    [Fact]
    public void CategoryPanel_StatusRegionHasAriaAtomic()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        Assert.True(
            markup.Contains("aria-atomic=\"true\""),
            "AtlasIntegrationCategoryPanel.razor must declare aria-atomic=\"true\" on the " +
            "validation status live region (SC 4.1.3 — whole region announced, not partial).");
    }

    [Fact]
    public void CategoryPanel_InvalidStatusUsesAlertRole()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        // Invalid + Unreachable are errors — they must use role="alert" (assertive).
        // Razor may emit this as a conditional expression like role="@(... ? "alert" : null)".
        Assert.True(
            markup.Contains("\"alert\""),
            "AtlasIntegrationCategoryPanel.razor must switch the status region to " +
            "role=\"alert\" for Invalid/Unreachable states (SC 4.1.3 — assertive announcement " +
            "for errors; polite is insufficient when credentials fail).");
    }

    [Fact]
    public void CategoryPanel_ValidStatusUsesPolite()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        // Razor may emit aria-live as a conditional like aria-live="@(... ? null : "polite")".
        Assert.True(
            markup.Contains("\"polite\""),
            "AtlasIntegrationCategoryPanel.razor must use aria-live=\"polite\" for " +
            "Valid/Unknown status (SC 4.1.3 — success announcements must not interrupt).");
    }

    [Fact]
    public void CategoryPanel_StatusIconsAreShapeDistinct()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        // SC 1.4.1: status must not rely on color alone — shape-distinct symbols required.
        // Connected=✓, Invalid=✕, Unreachable=⚠, Unknown=○
        Assert.True(
            markup.Contains("✓") || markup.Contains("&#x2713;"),
            "AtlasIntegrationCategoryPanel.razor must render a shape-distinct ✓ icon for " +
            "Connected status (SC 1.4.1 — status cannot be color-only).");

        Assert.True(
            markup.Contains("✕") || markup.Contains("&#x2715;") || markup.Contains("✗"),
            "AtlasIntegrationCategoryPanel.razor must render a shape-distinct ✕ or ✗ icon for " +
            "Invalid status (SC 1.4.1 — status cannot be color-only).");

        Assert.True(
            markup.Contains("⚠") || markup.Contains("&#x26A0;"),
            "AtlasIntegrationCategoryPanel.razor must render a shape-distinct ⚠ icon for " +
            "Unreachable status (SC 1.4.1 — status cannot be color-only).");
    }

    [Fact]
    public void CategoryPanel_PanelHasAriaBusy()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        Assert.True(
            markup.Contains("aria-busy="),
            "AtlasIntegrationCategoryPanel.razor must declare aria-busy on the panel " +
            "during validation (SC 4.1.3 — AT should not announce while results are in-flight).");
    }

    [Fact]
    public void CategoryPanel_ProviderSelectHasAriaLabel()
    {
        var markup = ReadSource("AtlasIntegrationCategoryPanel.razor");

        Assert.True(
            markup.Contains("aria-label="),
            "AtlasIntegrationCategoryPanel.razor must declare aria-label on the provider " +
            "select element (SC 4.1.2 — select must have accessible name beyond the label text).");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AtlasCredentialField.razor — field-level a11y
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CredentialField_HelpTextUsesAriaDescribedBy()
    {
        var markup = ReadSource("AtlasCredentialField.razor");

        Assert.True(
            markup.Contains("aria-describedby="),
            "AtlasCredentialField.razor must wire help-text via aria-describedby (SC 3.3.2 — " +
            "help instructions must be programmatically associated with the input).");
    }

    [Fact]
    public void CredentialField_RequiredIndicatorIsScreenReaderAccessible()
    {
        var markup = ReadSource("AtlasCredentialField.razor");

        // The " *" must be aria-hidden; the text equivalent must be in an sr-only span
        Assert.True(
            markup.Contains("aria-hidden=\"true\""),
            "AtlasCredentialField.razor must hide the visual ' *' required marker via " +
            "aria-hidden=\"true\" (SC 1.3.1 — decorative punctuation must not be announced).");

        Assert.True(
            markup.Contains("(required)") || markup.Contains("sf-visually-hidden"),
            "AtlasCredentialField.razor must render an sr-only text equivalent for required " +
            "fields (SC 3.3.2 — required state must be communicated to screen reader users).");
    }

    [Fact]
    public void CredentialField_LeaveUnchangedPlaceholderForExistingSecrets()
    {
        var markup = ReadSource("AtlasCredentialField.razor");

        // SC 3.3.7: existing secret credential should be shown as "leave unchanged"
        // with a placeholder, never pre-populated with the actual value.
        Assert.True(
            markup.Contains("HasExistingValue") && markup.Contains("placeholder"),
            "AtlasCredentialField.razor must render a 'leave unchanged' placeholder when " +
            "HasExistingValue is true (SC 3.3.7 — redundant entry: existing value must not " +
            "be re-entered unless user actively changes it).");
    }

    [Fact]
    public void CredentialField_ShowHideToggleHasAriaPressedAndAriaControls()
    {
        var markup = ReadSource("AtlasCredentialField.razor");

        Assert.True(
            markup.Contains("aria-pressed="),
            "AtlasCredentialField.razor must declare aria-pressed on the Show/Hide secret " +
            "toggle button (SC 4.1.2 — toggle state must be announced by AT).");

        Assert.True(
            markup.Contains("aria-controls="),
            "AtlasCredentialField.razor must declare aria-controls on the Show/Hide button " +
            "pointing to the input id (SC 4.1.2 — button must declare what it controls).");
    }

    [Fact]
    public void CredentialField_AutocompleteAttributePresent()
    {
        var markup = ReadSource("AtlasCredentialField.razor");

        Assert.True(
            Regex.IsMatch(markup, @"autocomplete\s*="),
            "AtlasCredentialField.razor must bind the autocomplete attribute on form inputs " +
            "(SC 3.3.8 — autocomplete required for credential fields to enable password manager " +
            "and browser autofill support).");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helper
    // ═════════════════════════════════════════════════════════════════════════

    private static string ReadSource(string fileName)
    {
        const int MaxDepth = 12;
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; depth < MaxDepth && current is not null; depth++)
        {
            var candidate = Path.Combine(
                current.FullName,
                "accelerators", "anchor", "Components", "Pages", "Settings", "Integrations",
                fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            current = current.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate {fileName} — walked {MaxDepth} levels from {AppContext.BaseDirectory}. " +
            $"Expected path: accelerators/anchor/Components/Pages/Settings/Integrations/{fileName}");
    }
}
