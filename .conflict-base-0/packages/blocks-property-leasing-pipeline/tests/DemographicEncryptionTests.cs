using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Sunfish.Foundation.Macaroons;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Recovery.TenantKey;
using Xunit;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

public sealed class DemographicEncryptionTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Bundle(
        InMemoryLeasingPipelineService Svc,
        ITenantKeyProvider Keys,
        IFieldEncryptor Encryptor,
        IFieldDecryptor Decryptor,
        PublicListingId ListingId);

    private static Bundle NewService(TenantId tenant)
    {
        var rootKeys = new InMemoryRootKeyStore();
        rootKeys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(rootKeys);
        var listingId = new PublicListingId(Guid.NewGuid());
        var promoter = new MacaroonCapabilityPromoter(issuer, tenant, new[] { listingId });

        var tenantKeys = new InMemoryTenantKeyProvider();
        var encryptor = new TenantKeyProviderFieldEncryptor(tenantKeys);
        var decryptor = new TenantKeyProviderFieldDecryptor(tenantKeys);

        var svc = new InMemoryLeasingPipelineService(
            prospectPromoter: promoter,
            inquiryValidator: null,
            auditTrail: null,
            signer: null,
            tenantId: default,
            paymentGateway: null,
            fieldEncryptor: encryptor,
            time: null);
        return new Bundle(svc, tenantKeys, encryptor, decryptor, listingId);
    }

    private static async Task<ProspectId> ProvisionVerifiedProspectAsync(Bundle b, TenantId tenant, string email = "alice@example.com")
    {
        var inquiry = await ((IPublicInquiryService)b.Svc).SubmitInquiryAsync(
            new PublicInquiryRequest
            {
                Tenant = tenant,
                Listing = b.ListingId,
                ProspectName = "Alice",
                ProspectEmail = email,
                MessageBody = "test",
                ClientIp = TestIp,
                UserAgent = "test",
            },
            new AnonymousCapability { Token = "anon-1", IssuedAt = Now.AddMinutes(-1), ExpiresAt = Now.AddMinutes(30) },
            default);
        var prospect = await b.Svc.PromoteInquiryToProspectAsync(inquiry.Id, email, default);
        return prospect.Id;
    }

    private static SubmitApplicationRequest BuildRequest(TenantId tenant, ProspectId prospect, PublicListingId listing, DemographicProfileSubmission demographics) =>
        new()
        {
            Tenant = tenant,
            Prospect = prospect,
            Listing = listing,
            Facts = new DecisioningFacts
            {
                GrossMonthlyIncome = 5000m,
                IncomeSource = "Acme",
                YearsAtIncomeSource = 3,
                PriorEvictionDisclosed = false,
                ReferenceCount = 2,
                PriorLandlordNames = new[] { "Bob" },
                DependentCount = 1,
            },
            Demographics = demographics,
            ApplicationFee = Money.Usd(50m),
            Signature = new SignatureEventRef(Guid.NewGuid()),
        };

    private static async Task<string> DecryptAsync(IFieldDecryptor dec, EncryptedField? field, IDecryptCapability cap, TenantId tenant)
    {
        if (field is null) return string.Empty;
        var bytes = await dec.DecryptAsync(field.Value, cap, tenant, default);
        return Encoding.UTF8.GetString(bytes.Span);
    }

    [Fact]
    public async Task RoundTrip_AllFieldsEncryptAndDecryptToOriginal()
    {
        var b = NewService(TenantA);
        var prospect = await ProvisionVerifiedProspectAsync(b, TenantA);
        var submission = new DemographicProfileSubmission
        {
            RaceOrEthnicity = "Black or African American",
            NationalOrigin = "USA",
            Religion = "Atheist",
            Sex = "Female",
            DisabilityStatus = "None",
            FamilialStatus = "Single, no children",
            MaritalStatus = "Single",
            IncomeSourceType = "W2 employment",
        };

        var application = await b.Svc.SubmitApplicationAsync(
            BuildRequest(TenantA, prospect, b.ListingId, submission), default);

        var cap = new FixedDecryptCapability("cap-1", new ActorId("hud-reader"), TenantA, Now.AddHours(1));
        Assert.Equal("Black or African American", await DecryptAsync(b.Decryptor, application.Demographics.RaceOrEthnicity, cap, TenantA));
        Assert.Equal("USA", await DecryptAsync(b.Decryptor, application.Demographics.NationalOrigin, cap, TenantA));
        Assert.Equal("Atheist", await DecryptAsync(b.Decryptor, application.Demographics.Religion, cap, TenantA));
        Assert.Equal("Female", await DecryptAsync(b.Decryptor, application.Demographics.Sex, cap, TenantA));
        Assert.Equal("None", await DecryptAsync(b.Decryptor, application.Demographics.DisabilityStatus, cap, TenantA));
        Assert.Equal("Single, no children", await DecryptAsync(b.Decryptor, application.Demographics.FamilialStatus, cap, TenantA));
        Assert.Equal("Single", await DecryptAsync(b.Decryptor, application.Demographics.MaritalStatus, cap, TenantA));
        Assert.Equal("W2 employment", await DecryptAsync(b.Decryptor, application.Demographics.IncomeSourceType, cap, TenantA));
    }

    [Fact]
    public async Task NullField_RemainsNullInEncryptedRecord()
    {
        var b = NewService(TenantA);
        var prospect = await ProvisionVerifiedProspectAsync(b, TenantA);
        var submission = new DemographicProfileSubmission
        {
            RaceOrEthnicity = "Asian",
            // All other fields null — prospect declined to disclose
        };

        var application = await b.Svc.SubmitApplicationAsync(
            BuildRequest(TenantA, prospect, b.ListingId, submission), default);

        Assert.NotNull(application.Demographics.RaceOrEthnicity);
        Assert.Null(application.Demographics.NationalOrigin);
        Assert.Null(application.Demographics.Religion);
        Assert.Null(application.Demographics.Sex);
        Assert.Null(application.Demographics.DisabilityStatus);
        Assert.Null(application.Demographics.FamilialStatus);
        Assert.Null(application.Demographics.MaritalStatus);
        Assert.Null(application.Demographics.IncomeSourceType);
    }

    [Fact]
    public async Task EmptyStringField_TreatedAsNull()
    {
        var b = NewService(TenantA);
        var prospect = await ProvisionVerifiedProspectAsync(b, TenantA);
        var submission = new DemographicProfileSubmission { RaceOrEthnicity = string.Empty };

        var application = await b.Svc.SubmitApplicationAsync(
            BuildRequest(TenantA, prospect, b.ListingId, submission), default);

        Assert.Null(application.Demographics.RaceOrEthnicity);
    }

    [Fact]
    public async Task DifferentTenants_ProduceDifferentCiphertextForSamePlaintext()
    {
        var bA = NewService(TenantA);
        var bB = NewService(TenantB);
        bB.Keys.GetType(); // referenced
        // Use bA's tenant-key store across both services so the ciphertexts are
        // produced under the SAME key provider but different tenant contexts.
        var keys = new InMemoryTenantKeyProvider();
        var encryptor = new TenantKeyProviderFieldEncryptor(keys);

        // Verify same-plaintext-different-tenant produces different ciphertext via the encryptor directly.
        var ct1 = await encryptor.EncryptAsync(Encoding.UTF8.GetBytes("Hispanic"), TenantA, default);
        var ct2 = await encryptor.EncryptAsync(Encoding.UTF8.GetBytes("Hispanic"), TenantB, default);
        Assert.False(ct1.Ciphertext.Span.SequenceEqual(ct2.Ciphertext.Span));
    }

    [Fact]
    public async Task FieldEncryptorNotWired_DropsToAllNullDemographics()
    {
        var rootKeys = new InMemoryRootKeyStore();
        rootKeys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(rootKeys);
        var listingId = new PublicListingId(Guid.NewGuid());
        var promoter = new MacaroonCapabilityPromoter(issuer, TenantA, new[] { listingId });
        var svc = new InMemoryLeasingPipelineService(promoter, time: null);
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(
            new PublicInquiryRequest
            {
                Tenant = TenantA,
                Listing = listingId,
                ProspectName = "X",
                ProspectEmail = "x@example.com",
                MessageBody = "x",
                ClientIp = TestIp,
                UserAgent = "test",
            },
            new AnonymousCapability { Token = "a", IssuedAt = Now, ExpiresAt = Now.AddMinutes(30) },
            default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "x@example.com", default);

        var submission = new DemographicProfileSubmission
        {
            RaceOrEthnicity = "Mixed",
            Sex = "Male",
        };
        var application = await svc.SubmitApplicationAsync(
            BuildRequest(TenantA, prospect.Id, listingId, submission), default);

        Assert.Null(application.Demographics.RaceOrEthnicity);
        Assert.Null(application.Demographics.Sex);
    }

    [Fact]
    public async Task PlaintextNeverPersists_OnApplicationRecord()
    {
        var b = NewService(TenantA);
        var prospect = await ProvisionVerifiedProspectAsync(b, TenantA);
        var plaintext = "very-sensitive-demographic-value";
        var submission = new DemographicProfileSubmission { RaceOrEthnicity = plaintext };

        var application = await b.Svc.SubmitApplicationAsync(
            BuildRequest(TenantA, prospect, b.ListingId, submission), default);

        // Inspect the encrypted ciphertext bytes — plaintext bytes must not
        // appear verbatim in the ciphertext field.
        var encrypted = application.Demographics.RaceOrEthnicity;
        Assert.NotNull(encrypted);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        Assert.False(encrypted!.Value.Ciphertext.Span.IndexOf(plaintextBytes) >= 0,
            "Plaintext bytes leaked into the ciphertext field.");
    }

    [Fact]
    public async Task DemographicProfile_AllPropertiesAreEncryptedFieldNullable()
    {
        // Pin the type: every public property of DemographicProfile must be
        // EncryptedField? (the FHA-defense layout's "structurally
        // inaccessible to decisioning" claim is now type-system enforced).
        await Task.Yield();
        foreach (var prop in typeof(DemographicProfile).GetProperties())
        {
            Assert.True(
                prop.PropertyType == typeof(EncryptedField?),
                $"DemographicProfile.{prop.Name} must be EncryptedField? (got {prop.PropertyType}).");
        }
    }
}
