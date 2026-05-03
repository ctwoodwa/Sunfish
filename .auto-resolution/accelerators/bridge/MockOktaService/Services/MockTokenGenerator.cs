using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Diagnostics;
using Microsoft.IdentityModel.Tokens;

namespace MockOktaService.Services;

public sealed class MockTokenGenerator(
    ILogger<MockTokenGenerator> logger,
    MockSigningKeys signingKeys,
    IHttpContextAccessor httpContextAccessor) : IMockTokenGenerator
{

    public string GenerateToken(string issuer, string audience, string grantType, string? scope, string? username, string? password)
    {
        logger.LogInformation(
            "Generating token. CorrelationId={CorrelationId} GrantType={GrantType} AudienceHash={AudienceHash}",
            GetCorrelationId(),
            grantType,
            HashForLog(audience));

        string normalizedUser = string.IsNullOrWhiteSpace(username) ? "devuser@example.com" : username;
        (string? givenName, string? familyName, string? displayName) = DeriveNameParts(normalizedUser);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, normalizedUser),
            new(JwtRegisteredClaimNames.Email, normalizedUser),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.GivenName, givenName),
            new(JwtRegisteredClaimNames.FamilyName, familyName),
            new("preferred_username", normalizedUser),
            new("scope", scope ?? "openid email profile")
        };

        if (grantType == "client_credentials")
        {
            claims.Add(new("token_use", "access"));
        }
        else if (grantType == "password")
        {
            claims.Add(new("token_use", "access"));
            claims.Add(new("auth_time", ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds().ToString()));
        }

        DateTime expiryTime = DateTime.UtcNow.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiryTime,
            signingCredentials: signingKeys.SigningCredentials
        );

        string? tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        logger.LogInformation(
            "Token generated. CorrelationId={CorrelationId} SubjectHash={SubjectHash} ClaimsCount={ClaimsCount} ExpiryTimeUtc={ExpiryTimeUtc}",
            GetCorrelationId(),
            HashForLog(normalizedUser),
            claims.Count,
            expiryTime);

        return tokenString;
    }

    public string GenerateAccessToken(string issuer, string audience, string? scope, string? subject, string? email, string? name)
    {
        DateTime now = DateTime.UtcNow;
        string normalizedSubject = string.IsNullOrWhiteSpace(subject) ? "mock-user-id" : subject;
        string normalizedEmail = email ?? "mockuser@example.com";
        (string? givenName, string? familyName, string? displayName) = DeriveNameParts(normalizedEmail, name);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, normalizedSubject),
            new(JwtRegisteredClaimNames.Email, normalizedEmail),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.GivenName, givenName),
            new(JwtRegisteredClaimNames.FamilyName, familyName),
            new("preferred_username", normalizedEmail),
            new("scope", scope ?? "openid email profile"),
            new("token_use", "access")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: signingKeys.SigningCredentials
        );

        logger.LogInformation(
            "Access token generated. CorrelationId={CorrelationId} SubjectHash={SubjectHash} AudienceHash={AudienceHash} ClaimsCount={ClaimsCount}",
            GetCorrelationId(),
            HashForLog(normalizedSubject),
            HashForLog(audience),
            claims.Count);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateIdToken(string issuer, string audience, string? subject, string? email, string? name, string? nonce)
    {
        DateTime now = DateTime.UtcNow;
        string normalizedSubject = string.IsNullOrWhiteSpace(subject) ? "mock-user-id" : subject;
        string normalizedEmail = email ?? "mockuser@example.com";
        (string? givenName, string? familyName, string? displayName) = DeriveNameParts(normalizedEmail, name);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, normalizedSubject),
            new(JwtRegisteredClaimNames.Email, normalizedEmail),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.GivenName, givenName),
            new(JwtRegisteredClaimNames.FamilyName, familyName),
            new("preferred_username", normalizedEmail),
            new(JwtRegisteredClaimNames.Iat, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            claims.Add(new("nonce", nonce));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: signingKeys.SigningCredentials
        );

        logger.LogInformation(
            "ID token generated. CorrelationId={CorrelationId} SubjectHash={SubjectHash} AudienceHash={AudienceHash} ClaimsCount={ClaimsCount} HasNonce={HasNonce}",
            GetCorrelationId(),
            HashForLog(normalizedSubject),
            HashForLog(audience),
            claims.Count,
            !string.IsNullOrWhiteSpace(nonce));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token, string issuer, string audience)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(token, new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKeys.SigningKey,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(0)
            }, out _);

            logger.LogInformation(
                "Token validation successful. CorrelationId={CorrelationId} AudienceHash={AudienceHash}",
                GetCorrelationId(),
                HashForLog(audience));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Token validation failed. CorrelationId={CorrelationId} AudienceHash={AudienceHash} Reason={Reason}",
                GetCorrelationId(),
                HashForLog(audience),
                ex.Message);
            return false;
        }
    }

    public ClaimsPrincipal? TryGetUserClaims(string token, string issuer)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            ClaimsPrincipal? principal = tokenHandler.ValidateToken(token, new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKeys.SigningKey,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            logger.LogInformation(
                "UserInfo claims extracted. CorrelationId={CorrelationId} SubjectHash={SubjectHash}",
                GetCorrelationId(),
                HashForLog(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)));

            return principal;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "UserInfo token validation failed. CorrelationId={CorrelationId} Reason={Reason}",
                GetCorrelationId(),
                ex.Message);
            return null;
        }
    }

    private string GetCorrelationId()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("CorrelationId", out object? correlationId) == true &&
            correlationId is string correlationIdString &&
            !string.IsNullOrWhiteSpace(correlationIdString))
        {
            return correlationIdString;
        }

        return Activity.Current?.TraceId.ToString() ?? "(none)";
    }

    /// <summary>
    /// Derives given_name, family_name, and a display name from an email address.
    /// If an explicit display name is provided and is not just the email, it is used as-is.
    /// Otherwise parses "first.last@domain" into ("First", "Last", "First Last").
    /// </summary>
    private static (string GivenName, string FamilyName, string DisplayName) DeriveNameParts(string email, string? explicitName = null)
    {
        // If an explicit display name was provided and it's not just the email repeated, use it.
        if (!string.IsNullOrWhiteSpace(explicitName) &&
            !string.Equals(explicitName, email, StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = explicitName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2
                ? (parts[0], parts[1], explicitName)
                : (explicitName, string.Empty, explicitName);
        }

        // Parse "first.last@domain" into name parts.
        string localPart = email.Contains('@') ? email[..email.IndexOf('@')] : email;
        string[] segments = localPart.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            string given = Capitalize(segments[0]);
            string family = Capitalize(segments[^1]);
            return (given, family, $"{given} {family}");
        }

        string single = Capitalize(localPart);
        return (single, string.Empty, single);

        static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    private static string HashForLog(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "(empty)";
        }

        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash[..6]);
    }
}
