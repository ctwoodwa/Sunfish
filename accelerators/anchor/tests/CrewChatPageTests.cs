using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#59 Phase 5 — Anchor consumer page contract tests. The Anchor MAUI
/// csproj targets <c>net11.0-{windows,maccatalyst}</c> with workload
/// requirements that the unit-test project deliberately doesn't import
/// (per the existing tests.csproj convention: compile MAUI-free source
/// files directly + ProjectReference the substrate packages). The page
/// surface is verified here via reflection on the generated component
/// class — bUnit-driven behavioural tests would require dragging in the
/// MAUI workload, which would regress the test project's MAUI-free
/// posture established in earlier waves.
/// <para>
/// The end-to-end behaviour (empty-state when ActiveTeam.Active is null,
/// SunfishChat composition when a team is active, navigation to /teams
/// from the empty-state button) is verifiable manually on the demo
/// (target Mac↔Mac 2026-05-08) and would benefit from a follow-up bUnit
/// test ride once the test project is split into MAUI-free + MAUI-ridden
/// halves.
/// </para>
/// </summary>
public sealed class CrewChatPageTests
{
    private static Type LoadCrewChatPageTypeOrFail()
    {
        // The generated component class is `Sunfish.Anchor.Components.Pages.CrewChatPage`
        // emitted by the Razor source generator from CrewChatPage.razor.
        // The Anchor MAUI csproj is NOT ProjectReferenced by tests.csproj
        // (per the existing MAUI-free-test convention), so we load the
        // built assembly via path probe alongside the test bin output.
        var alreadyLoaded = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            a => a.GetName().Name == "Sunfish.Anchor");
        if (alreadyLoaded is { } loaded)
        {
            return loaded.GetType("Sunfish.Anchor.Components.Pages.CrewChatPage")
                ?? throw new InvalidOperationException(
                    "Sunfish.Anchor is loaded but CrewChatPage type is missing — possible rename regression.");
        }

        // Probe for the Anchor build output. The Anchor csproj multi-targets
        // net11.0-{windows,maccatalyst}; the test's net11.0 base TFM doesn't
        // resolve a peer DLL automatically, so walk up to the repo root and
        // search the Anchor bin directory for any TFM-flavoured output.
        var testDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(testDir);
        DirectoryInfo? anchorBin = null;
        while (current is not null && anchorBin is null)
        {
            var candidate = Path.Combine(current.FullName, "accelerators", "anchor", "bin");
            if (Directory.Exists(candidate))
            {
                anchorBin = new DirectoryInfo(candidate);
                break;
            }
            current = current.Parent;
        }

        if (anchorBin is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Anchor bin directory walking up from '{testDir}'.");
        }

        var dlls = anchorBin
            .EnumerateFiles("Sunfish.Anchor.dll", SearchOption.AllDirectories)
            .ToList();
        if (dlls.Count == 0)
        {
            throw new InvalidOperationException(
                $"Sunfish.Anchor.dll not found under {anchorBin.FullName}. Build accelerators/anchor before running this test.");
        }

        var assembly = Assembly.LoadFrom(dlls[0].FullName);
        return assembly.GetType("Sunfish.Anchor.Components.Pages.CrewChatPage")
            ?? throw new InvalidOperationException(
                $"CrewChatPage type missing from {dlls[0].FullName} — possible rename or @page directive regression.");
    }

    [Fact]
    public void CrewChatPage_RouteAttribute_RegistersChatRoute()
    {
        var pageType = LoadCrewChatPageTypeOrFail();

        var routeAttrs = pageType.GetCustomAttributes<RouteAttribute>().ToArray();
        Assert.Contains(routeAttrs, a => a.Template == "/chat");
    }

    [Fact]
    public void CrewChatPage_HasRequiredInjectableProperties()
    {
        var pageType = LoadCrewChatPageTypeOrFail();

        var injectableNames = pageType
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Must inject every service the page consumes; missing any of
        // these would surface as a runtime DI resolution failure when
        // the operator navigates to /chat.
        Assert.Contains("ActiveTeam", injectableNames);
        Assert.Contains("ChannelProvider", injectableNames);
        Assert.Contains("CrewRoster", injectableNames);
        Assert.Contains("InvitationBus", injectableNames);
        Assert.Contains("Nav", injectableNames);
    }

    [Fact]
    public void CrewChatPage_DerivesFromComponentBase()
    {
        var pageType = LoadCrewChatPageTypeOrFail();

        Assert.True(typeof(ComponentBase).IsAssignableFrom(pageType),
            $"{pageType.FullName} must derive from ComponentBase to participate in the Blazor render tree.");
    }
}
