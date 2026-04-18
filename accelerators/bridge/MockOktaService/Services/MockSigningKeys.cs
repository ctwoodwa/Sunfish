using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MockOktaService.Services;

public sealed class MockSigningKeys
{
    private readonly RsaSecurityKey _signingKey;

    public MockSigningKeys()
    {
        var rsa = RSA.Create(2048);
        _signingKey = new(rsa)
        {
            KeyId = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16))
        };
    }

    public SecurityKey SigningKey => _signingKey;

    public SigningCredentials SigningCredentials => new(_signingKey, SecurityAlgorithms.RsaSha256);

    public JsonWebKeySet GetJwks()
    {
        JsonWebKey? jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_signingKey);
        var jwks = new JsonWebKeySet();
        jwks.Keys!.Add(jwk);
        return jwks;
    }
}


