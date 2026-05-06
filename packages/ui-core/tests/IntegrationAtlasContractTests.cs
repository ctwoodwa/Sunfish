using System;
using System.Linq;
using System.Reflection;
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
}
