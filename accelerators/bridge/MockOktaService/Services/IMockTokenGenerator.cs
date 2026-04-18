using System.Security.Claims;

namespace MockOktaService.Services;

public interface IMockTokenGenerator
{
    string GenerateToken(string issuer, string audience, string grantType, string? scope, string? username, string? password);
    string GenerateAccessToken(string issuer, string audience, string? scope, string? subject, string? email, string? name);
    string GenerateIdToken(string issuer, string audience, string? subject, string? email, string? name, string? nonce);
    bool ValidateToken(string token, string issuer, string audience);
    ClaimsPrincipal? TryGetUserClaims(string token, string issuer);
}
