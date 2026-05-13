using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public sealed class LiveRegionTests : BunitContext
{
    private static readonly TenantId Tenant = new("tenant-test");
    private static readonly ActorId Actor = new("actor-bob");

    private static readonly PermissionDecision AnyGranted = new PermissionDecision.Granted(
        ShipRole.Captain, DateTimeOffset.UtcNow, Proof: null);

    private static readonly PermissionDecision AnyDenied = new PermissionDecision.Denied(
        DenialReason.NoMatchingRole,
        "Role insufficient.",
        new Remediation(RemediationKind.ContactAuthority, "Contact the Captain.", ContactActor: null, EscalationLink: null, CallToActionLabel: null),
        DateTimeOffset.UtcNow);

    private static ShipsOfficeDocumentView MakeDraftDoc() =>
        new(new ShipsOfficeDocumentId("doc-1"),
            ShipsOfficeDocumentKind.BundleManifest,
            "Budget Report",
            DocumentStatus.Draft,
            DateTimeOffset.UtcNow,
            Actor,
            VersionLabel: null);

    private void RegisterServices(
        IPermissionResolver permResolver,
        IShipsOfficeDataProvider dataProvider,
        IShipsOfficeCommandService commandService)
    {
        var actorResolver = new InMemoryActorPrincipalResolver();
        actorResolver.Register(Actor, new Individual(PrincipalId.FromBytes(new byte[32])));
        Services.AddSingleton<IActorPrincipalResolver>(actorResolver);
        Services.AddSingleton(permResolver);
        Services.AddSingleton(dataProvider);
        Services.AddSingleton(commandService);
        Services.AddSingleton(Substitute.For<ISearchAsYouType<ShipsOfficeDocumentView>>());
    }

    [Fact]
    public async Task Publish_confirmation_announces_via_aria_live_polite()
    {
        var doc = MakeDraftDoc();
        var snapshot = new ShipsOfficeSnapshot([doc], 1, DateTimeOffset.UtcNow);

        var permResolver = Substitute.For<IPermissionResolver>();
        permResolver.ResolveAsync(
            Arg.Any<TenantId>(), Arg.Any<Principal>(),
            Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
            Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AnyGranted));

        var dataProvider = Substitute.For<IShipsOfficeDataProvider>();
        dataProvider.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(snapshot));

        var commandService = Substitute.For<IShipsOfficeCommandService>();
        commandService.PublishAsync(Arg.Any<TenantId>(), Arg.Any<ShipsOfficeDocumentId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PublishOutcome.Published));

        RegisterServices(permResolver, dataProvider, commandService);

        var cut = Render<ShipsOfficeBlock>(p => p
            .Add(c => c.Tenant, Tenant)
            .Add(c => c.Actor, Actor));

        cut.WaitForState(() => cut.Markup.Contains("Budget Report"), TimeSpan.FromSeconds(5));

        cut.Find(".ships-office-list-item").Click();
        cut.WaitForState(() => cut.Markup.Contains("btn-publish"), TimeSpan.FromSeconds(5));

        cut.Find(".btn-publish").Click();
        cut.WaitForState(() => cut.Markup.Contains("published successfully"), TimeSpan.FromSeconds(5));

        Assert.Contains("published successfully", cut.Markup, StringComparison.OrdinalIgnoreCase);
        var politeRegions = cut.FindAll("[aria-live='polite']");
        Assert.True(politeRegions.Any(r => r.TextContent.Contains("published", StringComparison.OrdinalIgnoreCase)),
            "Expected at least one aria-live='polite' region to announce the publish confirmation.");
    }

    [Fact]
    public async Task Permission_rejected_announces_via_aria_live_assertive()
    {
        var doc = MakeDraftDoc();
        var snapshot = new ShipsOfficeSnapshot([doc], 1, DateTimeOffset.UtcNow);

        var permResolver = Substitute.For<IPermissionResolver>();
        permResolver.ResolveAsync(
            Arg.Any<TenantId>(), Arg.Any<Principal>(),
            Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
            Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AnyGranted));

        var dataProvider = Substitute.For<IShipsOfficeDataProvider>();
        dataProvider.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(snapshot));

        var commandService = Substitute.For<IShipsOfficeCommandService>();
        commandService.PublishAsync(Arg.Any<TenantId>(), Arg.Any<ShipsOfficeDocumentId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PublishOutcome.Rejected));

        RegisterServices(permResolver, dataProvider, commandService);

        var cut = Render<ShipsOfficeBlock>(p => p
            .Add(c => c.Tenant, Tenant)
            .Add(c => c.Actor, Actor));

        cut.WaitForState(() => cut.Markup.Contains("Budget Report"), TimeSpan.FromSeconds(5));

        cut.Find(".ships-office-list-item").Click();
        cut.WaitForState(() => cut.Markup.Contains("btn-publish"), TimeSpan.FromSeconds(5));

        cut.Find(".btn-publish").Click();
        cut.WaitForState(() => cut.Markup.Contains("assertive"), TimeSpan.FromSeconds(5));

        var alertRegion = cut.Find("[aria-live='assertive']");
        Assert.NotNull(alertRegion);
        Assert.Contains("permission denied", alertRegion.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }
}
