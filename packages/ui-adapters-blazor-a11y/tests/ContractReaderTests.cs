using System.IO;
using System.Linq;
using Sunfish.UIAdapters.Blazor.A11y;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Plan 4 Task 1.6 — ContractReader tests against the actual a11y-contracts.json
/// emitted by the ui-core build pipeline. Exercises round-trip from JS-side
/// `parameters.a11y.sunfish` through `export-a11y-contracts.mts` to the .NET
/// SunfishA11yContract record.
/// </summary>
public class ContractReaderTests
{
    [Fact]
    public void Load_RoundTripsFromExportedContractsJson()
    {
        var contractsPath = ContractReaderFixture.RealContractsJsonPath();
        if (contractsPath is null)
        {
            // ui-core build hasn't run; the test is informative but not blocking. Report and skip.
            return;
        }

        var reader = new ContractReader(contractsPath);
        var tags = reader.AllTags().ToList();

        Assert.Contains("sunfish-button", tags);
        Assert.Contains("sunfish-dialog", tags);
        Assert.Contains("sunfish-syncstate-indicator", tags);
    }

    [Fact]
    public void Load_DeserializesButtonContractFully()
    {
        var contractsPath = ContractReaderFixture.RealContractsJsonPath();
        if (contractsPath is null) return;

        var reader = new ContractReader(contractsPath);
        var button = reader.Load("sunfish-button");

        Assert.Equal("https://www.w3.org/WAI/ARIA/apg/patterns/button/", button.AriaPattern);
        Assert.Contains("1.3.1", button.Wcag22Conformant);
        Assert.Contains("4.1.2", button.Wcag22Conformant);
        Assert.Equal(2, button.KeyboardMap.Count);
        Assert.Equal("activate", button.KeyboardMap[0].Action);
        Assert.Contains("Enter", button.KeyboardMap[0].Keys);
        Assert.Equal("self", button.Focus.Initial);
        Assert.False(button.Focus.Trap);
        Assert.Null(button.Focus.Restore);
    }

    [Fact]
    public void Load_DeserializesDialogContractWithFocusTrap()
    {
        var contractsPath = ContractReaderFixture.RealContractsJsonPath();
        if (contractsPath is null) return;

        var reader = new ContractReader(contractsPath);
        var dialog = reader.Load("sunfish-dialog");

        Assert.True(dialog.Focus.Trap);
        Assert.Equal("first-focusable-child", dialog.Focus.Initial);
        Assert.Equal("trigger", dialog.Focus.Restore);
        Assert.Contains("sunfish-button", dialog.ComposedOf);
        // Escape, Tab, Shift+Tab keyboard bindings.
        Assert.Equal(3, dialog.KeyboardMap.Count);
    }

    [Fact]
    public void Load_DeserializesSyncStateContractWithDirectionalIcons()
    {
        var contractsPath = ContractReaderFixture.RealContractsJsonPath();
        if (contractsPath is null) return;

        var reader = new ContractReader(contractsPath);
        var sync = reader.Load("sunfish-syncstate-indicator");

        Assert.Contains("conflict", sync.DirectionalIcons);
        Assert.Empty(sync.KeyboardMap); // Indicator is non-interactive.
    }

    [Fact]
    public void Load_ThrowsKeyNotFoundForUnknownTag()
    {
        var contractsPath = ContractReaderFixture.RealContractsJsonPath();
        if (contractsPath is null) return;

        var reader = new ContractReader(contractsPath);
        var ex = Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => reader.Load("sunfish-does-not-exist"));
        Assert.Contains("sunfish-does-not-exist", ex.Message);
    }

    [Fact]
    public void TryLoad_ReturnsFalseWithoutThrowing()
    {
        var contractsPath = ContractReaderFixture.RealContractsJsonPath();
        if (contractsPath is null) return;

        var reader = new ContractReader(contractsPath);
        var found = reader.TryLoad("sunfish-does-not-exist", out var contract);
        Assert.False(found);
        Assert.Null(contract);
    }

    [Fact]
    public void Load_DeserializesSyntheticContractFromMemory()
    {
        // Synthetic test that does not depend on the ui-core build having run.
        var tmp = Path.Combine(Path.GetTempPath(), $"sunfish-contracts-{System.Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tmp, """
                {
                  "test-component": {
                    "ariaPattern": "https://example.com/test",
                    "keyboardMap": [
                      { "keys": ["Enter"], "action": "submit" }
                    ],
                    "focus": { "initial": "self", "trap": false, "restore": null },
                    "directionalIcons": ["arrow-back"],
                    "wcag22Conformant": ["1.1.1"]
                  }
                }
                """);

            var reader = new ContractReader(tmp);
            var contract = reader.Load("test-component");

            Assert.Equal("https://example.com/test", contract.AriaPattern);
            Assert.Single(contract.KeyboardMap);
            Assert.Equal("submit", contract.KeyboardMap[0].Action);
            Assert.Equal("self", contract.Focus.Initial);
            Assert.Contains("arrow-back", contract.DirectionalIcons);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void Load_ThrowsFileNotFoundWithGuidance()
    {
        var reader = new ContractReader(Path.Combine(Path.GetTempPath(), "nonexistent.json"));
        var ex = Assert.Throws<FileNotFoundException>(() => reader.Load("anything"));
        Assert.Contains("pnpm --filter @sunfish/ui-core build:contracts", ex.Message);
    }
}

internal static class ContractReaderFixture
{
    /// <summary>
    /// Locate the real a11y-contracts.json from the ui-core build, walking up from
    /// the test executable's directory. Returns null if the build hasn't run.
    /// </summary>
    public static string? RealContractsJsonPath()
    {
        var current = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(current); i++)
        {
            var candidate = Path.Combine(current, "packages", "ui-core", "dist", "a11y-contracts.json");
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current);
        }
        return null;
    }
}
