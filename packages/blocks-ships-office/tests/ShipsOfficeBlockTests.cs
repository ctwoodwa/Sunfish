using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public sealed class ShipsOfficeBlockTests : BunitContext
{
    private static readonly TenantId Tenant = new("tenant-test");
    private static readonly ActorId Actor = new("actor-alice");

    private static readonly ShipsOfficeSnapshot EmptySnapshot = new(
        Documents: Array.Empty<ShipsOfficeDocumentView>(),
        TotalCount: 0,
        AsOf: DateTimeOffset.UtcNow);

    private static readonly PermissionDecision Granted = new PermissionDecision.Granted(
        ShipRole.Captain,
        DateTimeOffset.UtcNow,
        Proof: null);

    private static readonly PermissionDecision Denied = new PermissionDecision.Denied(
        DenialReason.NoMatchingRole,
        "Role insufficient.",
        new Remediation(RemediationKind.ContactAuthority, "Contact the Captain.", ContactActor: null, EscalationLink: null, CallToActionLabel: null),
        DateTimeOffset.UtcNow);

    private InMemoryActorPrincipalResolver MakeResolver(bool registerActor = true)
    {
        var resolver = new InMemoryActorPrincipalResolver();
        if (registerActor)
        {
            var principalId = PrincipalId.FromBytes(new byte[32]);
            resolver.Register(Actor, new Individual(principalId));
        }
        return resolver;
    }

    private IPermissionResolver AllGrant()
    {
        var r = Substitute.For<IPermissionResolver>();
        r.ResolveAsync(
            Arg.Any<TenantId>(), Arg.Any<Principal>(),
            Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
            Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Granted));
        return r;
    }

    private IPermissionResolver DenyAction(ShipAction action)
    {
        var r = Substitute.For<IPermissionResolver>();
        r.ResolveAsync(
            Arg.Any<TenantId>(), Arg.Any<Principal>(),
            Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
            Arg.Is(action), Arg.Any<Resource?>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Denied));
        r.ResolveAsync(
            Arg.Any<TenantId>(), Arg.Any<Principal>(),
            Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
            Arg.Is<ShipAction>(a => a != action), Arg.Any<Resource?>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Granted));
        return r;
    }

    private IShipsOfficeDataProvider DataProviderWith(ShipsOfficeSnapshot snapshot)
    {
        var dp = Substitute.For<IShipsOfficeDataProvider>();
        dp.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(snapshot));
        return dp;
    }

    private void RegisterServices(
        IActorPrincipalResolver? actorResolver = null,
        IPermissionResolver? permResolver = null,
        IShipsOfficeDataProvider? dataProvider = null,
        IShipsOfficeCommandService? commandService = null)
    {
        Services.AddSingleton(actorResolver ?? MakeResolver());
        Services.AddSingleton(permResolver ?? AllGrant());
        Services.AddSingleton(dataProvider ?? DataProviderWith(EmptySnapshot));
        Services.AddSingleton(commandService ?? Substitute.For<IShipsOfficeCommandService>());
        Services.AddSingleton(Substitute.For<ISearchAsYouType<ShipsOfficeDocumentView>>());
    }

    [Fact]
    public void Block_renders_only_when_actor_has_ViewShipsOffice()
    {
        RegisterServices(actorResolver: MakeResolver(registerActor: false));

        var cut = Render<ShipsOfficeBlock>(p => p
            .Add(c => c.Tenant, Tenant)
            .Add(c => c.Actor, Actor));

        cut.WaitForState(
            () => cut.Markup.Contains("permission") || cut.Markup.Contains("document"),
            TimeSpan.FromSeconds(5));

        Assert.Contains("permission", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ships-office-document-list", cut.Markup);
    }

    [Fact]
    public void Block_invokes_IPermissionResolver_before_calling_DataProvider()
    {
        var callOrder = new List<string>();

        var permResolver = Substitute.For<IPermissionResolver>();
        permResolver.ResolveAsync(
            Arg.Any<TenantId>(), Arg.Any<Principal>(),
            Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
            Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
            Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("permission");
                return ValueTask.FromResult(Granted);
            });

        var dataProvider = Substitute.For<IShipsOfficeDataProvider>();
        dataProvider.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("data");
                return Task.FromResult(EmptySnapshot);
            });

        RegisterServices(permResolver: permResolver, dataProvider: dataProvider);

        var cut = Render<ShipsOfficeBlock>(p => p
            .Add(c => c.Tenant, Tenant)
            .Add(c => c.Actor, Actor));

        cut.WaitForState(
            () => callOrder.Contains("data"),
            TimeSpan.FromSeconds(5));

        var permIdx = callOrder.IndexOf("permission");
        var dataIdx = callOrder.IndexOf("data");
        Assert.True(permIdx >= 0 && dataIdx >= 0, "Both calls must be recorded.");
        Assert.True(permIdx < dataIdx, "Permission must be resolved before data is loaded.");
    }

    [Fact]
    public void Publish_button_hidden_when_actor_lacks_PublishShipsOfficeDocument()
    {
        var doc = new ShipsOfficeDocumentView(
            new ShipsOfficeDocumentId("doc-1"),
            ShipsOfficeDocumentKind.BundleManifest,
            "Test Bundle",
            DocumentStatus.Draft,
            DateTimeOffset.UtcNow,
            Actor,
            VersionLabel: null);

        var snapshot = new ShipsOfficeSnapshot([doc], 1, DateTimeOffset.UtcNow);
        RegisterServices(
            permResolver: DenyAction(ShipAction.PublishShipsOfficeDocument),
            dataProvider: DataProviderWith(snapshot));

        var cut = Render<ShipsOfficeBlock>(p => p
            .Add(c => c.Tenant, Tenant)
            .Add(c => c.Actor, Actor));

        cut.WaitForState(
            () => cut.Markup.Contains("Test Bundle"),
            TimeSpan.FromSeconds(5));

        cut.Find(".ships-office-list-item").Click();

        cut.WaitForState(
            () => cut.Markup.Contains("drawer-actions"),
            TimeSpan.FromSeconds(5));

        Assert.DoesNotContain("btn-publish", cut.Markup);
    }

    [Fact]
    public void Archive_button_hidden_when_actor_lacks_ArchiveShipsOfficeDocument()
    {
        var doc = new ShipsOfficeDocumentView(
            new ShipsOfficeDocumentId("doc-1"),
            ShipsOfficeDocumentKind.BundleManifest,
            "Published Bundle",
            DocumentStatus.Published,
            DateTimeOffset.UtcNow,
            Actor,
            VersionLabel: null);

        var snapshot = new ShipsOfficeSnapshot([doc], 1, DateTimeOffset.UtcNow);
        RegisterServices(
            permResolver: DenyAction(ShipAction.ArchiveShipsOfficeDocument),
            dataProvider: DataProviderWith(snapshot));

        var cut = Render<ShipsOfficeBlock>(p => p
            .Add(c => c.Tenant, Tenant)
            .Add(c => c.Actor, Actor));

        cut.WaitForState(
            () => cut.Markup.Contains("Published Bundle"),
            TimeSpan.FromSeconds(5));

        cut.Find(".ships-office-list-item").Click();

        cut.WaitForState(
            () => cut.Markup.Contains("drawer-actions"),
            TimeSpan.FromSeconds(5));

        Assert.DoesNotContain("btn-archive", cut.Markup);
    }
}
