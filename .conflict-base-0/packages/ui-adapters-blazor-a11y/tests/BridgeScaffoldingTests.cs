using System.Text.Json;
using Sunfish.UIAdapters.Blazor.A11y;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Smoke tests confirming the Task-1.1 scaffold compiles, references resolve, and the
/// <see cref="AxeResult"/> projection deserialises a representative axe-core 4.x response.
/// The substantive bridge tests (Playwright lifecycle, axe invocation, contract assertions,
/// 36-scenario matrix) land in Tasks 1.2 – 1.7.
/// </summary>
public class BridgeScaffoldingTests
{
    [Fact]
    public void AxeResult_DeserializesEmptyResponse()
    {
        const string json = """
        { "violations": [], "passes": [], "incomplete": [], "inapplicable": [] }
        """;
        var result = JsonSerializer.Deserialize<AxeResult>(json);
        Assert.NotNull(result);
        Assert.Empty(result!.Violations);
        Assert.Empty(result.Passes);
    }

    [Fact]
    public void AxeResult_DeserializesViolationWithImpact()
    {
        const string json = """
        {
          "violations": [{
            "id": "color-contrast",
            "impact": "serious",
            "description": "Elements must meet minimum color contrast",
            "help": "Elements must have sufficient color contrast",
            "helpUrl": "https://dequeuniversity.com/rules/axe/4.10/color-contrast",
            "tags": ["cat.color", "wcag2aa", "wcag143"],
            "actIds": ["abc-123"],
            "nodes": [{
              "target": ["#btn-submit"],
              "html": "<button id=\"btn-submit\">OK</button>",
              "failureSummary": "Fix any of the following: Element has insufficient color contrast"
            }]
          }],
          "passes": [],
          "incomplete": [],
          "inapplicable": []
        }
        """;
        var result = JsonSerializer.Deserialize<AxeResult>(json);
        Assert.NotNull(result);
        Assert.Single(result!.Violations);
        var v = result.Violations[0];
        Assert.Equal("color-contrast", v.Id);
        Assert.Equal(AxeImpact.Serious, v.Impact);
        Assert.Contains("wcag2aa", v.GetTagStrings());
        Assert.Single(v.Nodes);
        Assert.Equal("#btn-submit", v.Nodes[0].Target[0]);
    }

    [Fact]
    public void AxeImpact_OrderingAllowsModeratePlusFilter()
    {
        // Filter idiom for "moderate or worse" that callers will use.
        var threshold = AxeImpact.Moderate;
        Assert.True(AxeImpact.Moderate >= threshold);
        Assert.True(AxeImpact.Serious >= threshold);
        Assert.True(AxeImpact.Critical >= threshold);
        Assert.False(AxeImpact.Minor >= threshold);
    }
}
