using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// W#48 Phase 1a — Atlas Integration-Config contract surface tests
/// per ADR 0067.
/// </summary>
public class IntegrationAtlasContractTests
{
    [Fact]
    public void IIntegrationProviderValidator_NoMethodReturnsDecryptedBytes()
    {
        // §Trust contract test (per W#48 hand-off Phase 1 §Tests):
        // no method on the validator surface may return raw decrypted
        // bytes. The validator RECEIVES decrypted bytes (via the
        // sensitiveCredentials parameter) but never returns them.
        var t = typeof(IIntegrationProviderValidator);
        var disallowed = new[]
        {
            typeof(byte[]),
            typeof(System.ReadOnlyMemory<byte>),
            typeof(System.Memory<byte>),
        };
        foreach (var method in t.GetMethods())
        {
            // Inspect the unwrapped return type (Task<T> → T).
            var rt = method.ReturnType;
            if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
            {
                rt = rt.GetGenericArguments()[0];
            }
            Assert.DoesNotContain(rt, disallowed);

            // Method names containing "decrypt" or "credential" with a
            // string return type would also be a §Trust regression.
            var name = method.Name.ToLowerInvariant();
            if (rt == typeof(string) && (name.Contains("decrypt") || name.Contains("credential")))
            {
                Assert.Fail($"{method.Name} returns string and looks like a decrypted credential leak path.");
            }
        }
    }

    [Fact]
    public void IntegrationCategory_HasExactlySixValues()
    {
        var values = Enum.GetValues<IntegrationCategory>();
        Assert.Equal(6, values.Length);
        Assert.Contains(IntegrationCategory.Payments, values);
        Assert.Contains(IntegrationCategory.TransactionalEmail, values);
        Assert.Contains(IntegrationCategory.MarketingEmail, values);
        Assert.Contains(IntegrationCategory.Messaging, values);
        Assert.Contains(IntegrationCategory.MeshVpn, values);
        Assert.Contains(IntegrationCategory.Captcha, values);
    }

    [Fact]
    public void CredentialAutocompleteHint_HasExactlySevenValues()
    {
        // Every value must be a WHATWG-canonical autocomplete token.
        var values = Enum.GetValues<CredentialAutocompleteHint>();
        Assert.Equal(7, values.Length);
        Assert.Contains(CredentialAutocompleteHint.None, values);
        Assert.Contains(CredentialAutocompleteHint.CurrentPassword, values);
        Assert.Contains(CredentialAutocompleteHint.NewPassword, values);
        Assert.Contains(CredentialAutocompleteHint.OneTimeCode, values);
        Assert.Contains(CredentialAutocompleteHint.Username, values);
        Assert.Contains(CredentialAutocompleteHint.Email, values);
        Assert.Contains(CredentialAutocompleteHint.Url, values);
    }

    [Fact]
    public void CredentialFieldKind_HasExactlyFourValues()
    {
        var values = Enum.GetValues<CredentialFieldKind>();
        Assert.Equal(4, values.Length);
        Assert.Contains(CredentialFieldKind.Text, values);
        Assert.Contains(CredentialFieldKind.Secret, values);
        Assert.Contains(CredentialFieldKind.Url, values);
        Assert.Contains(CredentialFieldKind.ReadOnlyOutput, values);
    }

    [Fact]
    public void ProviderValidationStatus_HasExactlyFourValues()
    {
        var values = Enum.GetValues<ProviderValidationStatus>();
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void IntegrationCapabilityPurposes_HasIntegrationValidationConstant()
    {
        Assert.Equal("integration-validation",
            IntegrationCapabilityPurposes.IntegrationValidation);
    }

    [Fact]
    public void IIntegrationProviderValidator_IsHiddenFromIntelliSense()
    {
        var t = typeof(IIntegrationProviderValidator);
        var attr = t.GetCustomAttribute<System.ComponentModel.EditorBrowsableAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(System.ComponentModel.EditorBrowsableState.Never, attr!.State);
    }

    [Fact]
    public void IIntegrationAtlasContext_HasTenantAndActor()
    {
        var t = typeof(IIntegrationAtlasContext);
        Assert.NotNull(t.GetProperty("CurrentTenantId"));
        Assert.NotNull(t.GetProperty("CurrentActorId"));
    }

    [Fact]
    public void IValidationStatusStore_HasThreeMethods()
    {
        var t = typeof(IValidationStatusStore);
        Assert.NotNull(t.GetMethod("GetCurrentAsync"));
        Assert.NotNull(t.GetMethod("UpdateAsync"));
        Assert.NotNull(t.GetMethod("HistoryAsync"));
    }

    [Fact]
    public void CredentialFieldSpec_RequiredFlag_IsBool()
    {
        var prop = typeof(CredentialFieldSpec).GetProperty("IsRequired");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    [Fact]
    public void IntegrationValidationResult_TimeFieldIsDateTimeOffset()
    {
        var prop = typeof(IntegrationValidationResult).GetProperty("ValidatedAt");
        Assert.NotNull(prop);
        Assert.Equal(typeof(DateTimeOffset), prop!.PropertyType);
    }

    [Fact]
    public void IntegrationProviderSchema_CredentialFields_IsReadOnlyList()
    {
        var prop = typeof(IntegrationProviderSchema).GetProperty("CredentialFields");
        Assert.NotNull(prop);
        Assert.Equal(typeof(System.Collections.Generic.IReadOnlyList<CredentialFieldSpec>),
            prop!.PropertyType);
    }

    // ===== Phase 1b additions =====

    [Fact]
    public void IIntegrationAtlasProvider_NoMethodReturnsDecryptedBytes()
    {
        var t = typeof(IIntegrationAtlasProvider);
        var disallowed = new[]
        {
            typeof(byte[]),
            typeof(System.ReadOnlyMemory<byte>),
            typeof(System.Memory<byte>),
        };
        foreach (var method in t.GetMethods())
        {
            var rt = method.ReturnType;
            if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
            {
                rt = rt.GetGenericArguments()[0];
            }
            Assert.DoesNotContain(rt, disallowed);

            var nameLower = method.Name.ToLowerInvariant();
            if ((nameLower.Contains("decrypt") || nameLower.Contains("credential")) && rt == typeof(string))
            {
                Assert.Fail($"{method.Name} returns string + carries credential / decrypt semantics — §Trust regression.");
            }
        }
    }

    [Fact]
    public void IIntegrationAtlasProvider_ExtendsIAtlasProviderOfIntegrationAtlasView()
    {
        var t = typeof(IIntegrationAtlasProvider);
        Assert.Contains(
            typeof(Sunfish.UICore.Wayfinder.IAtlasProvider<IntegrationAtlasView>),
            t.GetInterfaces());
    }

    [Fact]
    public void IIntegrationAtlasProvider_IssueMethods_ReturnTaskOfStandingOrderId()
    {
        // Cycle-break: hand-off cited Task<StandingOrder>; cycle-safe
        // substitute is Task<StandingOrderId>.
        var t = typeof(IIntegrationAtlasProvider);
        foreach (var name in new[] {
            nameof(IIntegrationAtlasProvider.IssueProviderChangeAsync),
            nameof(IIntegrationAtlasProvider.IssueSensitiveCredentialAsync),
            nameof(IIntegrationAtlasProvider.IssueNonSensitiveCredentialAsync),
            nameof(IIntegrationAtlasProvider.IssueRoutingAsync),
        })
        {
            var m = t.GetMethod(name);
            Assert.NotNull(m);
            var rt = m!.ReturnType;
            Assert.True(rt.IsGenericType);
            Assert.Equal(typeof(System.Threading.Tasks.Task<>), rt.GetGenericTypeDefinition());
            Assert.Equal(
                typeof(Sunfish.Foundation.Assets.Common.StandingOrderId),
                rt.GetGenericArguments()[0]);
        }
    }

    [Fact]
    public void IDecryptCapabilityProvider_AcquireAsync_ReturnsTaskOfNullableCapability()
    {
        var t = typeof(Sunfish.Foundation.Crypto.IDecryptCapabilityProvider);
        var m = t.GetMethod(nameof(Sunfish.Foundation.Crypto.IDecryptCapabilityProvider.AcquireAsync));
        Assert.NotNull(m);
        var rt = m!.ReturnType;
        Assert.True(rt.IsGenericType);
        Assert.Equal(typeof(System.Threading.Tasks.Task<>), rt.GetGenericTypeDefinition());
        Assert.Equal(
            typeof(Sunfish.Foundation.Crypto.IDecryptCapability),
            rt.GetGenericArguments()[0]);
    }

    [Fact]
    public void AddSunfishIntegrationAtlas_ThrowsWhenDecryptCapabilityProviderMissing()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddSunfishIntegrationAtlas());
        Assert.Contains("AddSunfishRecoveryCoordinator", ex.Message);
    }

    [Fact]
    public void AddSunfishIntegrationAtlas_RegistersValidationStatusStoreOnceWhenDecryptCapabilityPresent()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<Sunfish.Foundation.Crypto.IDecryptCapabilityProvider>(_ =>
            throw new NotSupportedException("placeholder; not invoked in this test"));

        services.AddSunfishIntegrationAtlas();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IValidationStatusStore>();
        Assert.IsType<InMemoryValidationStatusStore>(store);

        // Idempotency: second call leaves the existing registration intact.
        services.AddSunfishIntegrationAtlas();
        Assert.Single(services, d => d.ServiceType == typeof(IValidationStatusStore));
    }

    [Fact]
    public async System.Threading.Tasks.Task InMemoryValidationStatusStore_GetCurrent_RoundTripsLatestUpdate()
    {
        var store = new InMemoryValidationStatusStore();
        var tenant = Sunfish.Foundation.Assets.Common.TenantId.System;
        var category = IntegrationCategory.Payments;
        var providerId = "stripe";

        Assert.Null(await store.GetCurrentAsync(tenant, category, providerId));

        var result = new IntegrationValidationResult(
            ProviderValidationStatus.Valid,
            DateTimeOffset.UtcNow,
            null,
            null);
        await store.UpdateAsync(tenant, category, providerId, result, "alice");

        var current = await store.GetCurrentAsync(tenant, category, providerId);
        Assert.NotNull(current);
        Assert.Equal(ProviderValidationStatus.Valid, current!.Result.Status);
        Assert.Equal("alice", current.RecordedBy.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task InMemoryValidationStatusStore_HistoryAsync_ReturnsNewestFirstUpToCap()
    {
        var store = new InMemoryValidationStatusStore();
        var tenant = Sunfish.Foundation.Assets.Common.TenantId.System;
        var category = IntegrationCategory.MeshVpn;
        var providerId = "headscale";
        var baseAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        for (var i = 0; i < 5; i++)
        {
            await store.UpdateAsync(tenant, category, providerId,
                new IntegrationValidationResult(
                    ProviderValidationStatus.Valid,
                    baseAt.AddMinutes(i),
                    null,
                    null),
                "ops");
        }

        var collected = new System.Collections.Generic.List<ProviderValidationStatusEntry>();
        await foreach (var entry in store.HistoryAsync(tenant, category, providerId, maxEntries: 3))
        {
            collected.Add(entry);
        }

        Assert.Equal(3, collected.Count);
        Assert.True(collected[0].Result.ValidatedAt > collected[1].Result.ValidatedAt);
        Assert.True(collected[1].Result.ValidatedAt > collected[2].Result.ValidatedAt);
    }

    [Fact]
    public void NewAuditEventTypes_HaveExpectedStringValues()
    {
        Assert.Equal("IntegrationProviderChanged",
            Sunfish.Kernel.Audit.AuditEventType.IntegrationProviderChanged.Value);
        Assert.Equal("IntegrationCredentialUpdated",
            Sunfish.Kernel.Audit.AuditEventType.IntegrationCredentialUpdated.Value);
        Assert.Equal("IntegrationValidationSucceeded",
            Sunfish.Kernel.Audit.AuditEventType.IntegrationValidationSucceeded.Value);
        Assert.Equal("IntegrationValidationFailed",
            Sunfish.Kernel.Audit.AuditEventType.IntegrationValidationFailed.Value);
    }
}
