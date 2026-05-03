using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using MockOktaService.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<MockSigningKeys>();
builder.Services.AddScoped<IMockTokenGenerator, MockTokenGenerator>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

var authCodeLifetime = TimeSpan.FromMinutes(10);
var interactionLifetime = TimeSpan.FromMinutes(10);
bool enable2fa = app.Configuration.GetValue<bool>("MockOkta:Enable2fa");

app.Use(async (context, next) =>
{
    string incomingCorrelationId = context.Request.Headers["X-Correlation-ID"].ToString();
    string correlationId = string.IsNullOrWhiteSpace(incomingCorrelationId)
        ? (Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier)
        : incomingCorrelationId;

    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevCors");
}

app.MapDefaultEndpoints();

var authCodes = new ConcurrentDictionary<string, AuthCodeRecord>();
var pendingInteractions = new ConcurrentDictionary<string, PendingInteractionRecord>();

app.MapGet("/.well-known/openid-configuration", (HttpContext context, ILogger<Program> logger) =>
{
    string issuer = GetIssuer(context.Request);
    logger.LogInformation(
        "OIDC discovery request served. TraceId={TraceId} CorrelationId={CorrelationId} Issuer={Issuer}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        issuer);

    return Results.Ok(new
    {
        issuer,
        authorization_endpoint = $"{issuer}/oauth2/v1/authorize",
        token_endpoint = $"{issuer}/oauth2/v1/token",
        userinfo_endpoint = $"{issuer}/oauth2/v1/userinfo",
        end_session_endpoint = $"{issuer}/oauth2/v1/logout",
        jwks_uri = $"{issuer}/oauth2/v1/keys",
        introspection_endpoint = $"{issuer}/oauth2/v1/introspect",
        revocation_endpoint = $"{issuer}/oauth2/v1/revoke",
        response_types_supported = new[] { "code", "id_token", "code id_token", "code token", "id_token token", "code id_token token" },
        response_modes_supported = new[] { "query", "fragment", "form_post" },
        grant_types_supported = new[] { "authorization_code", "implicit", "refresh_token", "password", "client_credentials" },
        subject_types_supported = new[] { "public" },
        id_token_signing_alg_values_supported = new[] { "RS256" },
        scopes_supported = new[] { "openid", "email", "profile", "address", "phone", "offline_access", "groups" },
        token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post", "client_secret_jwt", "private_key_jwt", "none" },
        claims_supported = new[] { "iss", "sub", "aud", "iat", "exp", "jti", "auth_time", "amr", "idp", "nonce", "name", "nickname", "preferred_username", "given_name", "middle_name", "family_name", "email", "email_verified", "profile", "zoneinfo", "locale", "address", "phone_number", "picture", "website", "gender", "birthdate", "updated_at", "at_hash", "c_hash" },
        code_challenge_methods_supported = new[] { "S256" },
        introspection_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post", "none" },
        revocation_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post", "none" },
        request_parameter_supported = true,
        request_object_signing_alg_values_supported = new[] { "HS256", "HS384", "HS512", "RS256", "RS384", "RS512", "ES256", "ES384", "ES512" }
    });
})
.WithName("OidcDiscovery");

app.MapGet("/oauth2/v1/keys", (HttpContext context, MockSigningKeys signingKeys, ILogger<Program> logger) =>
{
    logger.LogInformation(
        "JWKS requested. TraceId={TraceId} CorrelationId={CorrelationId}",
        context.TraceIdentifier,
        GetCorrelationId(context));
    return Results.Ok(signingKeys.GetJwks());
})
   .WithName("Jwks");

app.MapGet("/oauth2/v1/userinfo", (HttpContext context, IMockTokenGenerator tokenGenerator, ILogger<Program> logger) =>
{
    string authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning(
            "UserInfo rejected: missing or non-Bearer Authorization header. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        return Results.Unauthorized();
    }

    string token = authorization["Bearer ".Length..].Trim();
    string issuer = GetIssuer(context.Request);
    ClaimsPrincipal? principal = tokenGenerator.TryGetUserClaims(token, issuer);

    if (principal is null)
    {
        logger.LogWarning(
            "UserInfo rejected: invalid token. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        return Results.Unauthorized();
    }

    string sub = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "mock-user-id";
    string email = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? sub;
    string name = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name)?.Value ?? sub;
    string givenName = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.GivenName)?.Value ?? string.Empty;
    string familyName = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.FamilyName)?.Value ?? string.Empty;

    logger.LogInformation(
        "UserInfo served. TraceId={TraceId} CorrelationId={CorrelationId} SubjectHash={SubjectHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        HashForLog(sub));

    return Results.Ok(new
    {
        sub,
        name,
        given_name = givenName,
        family_name = familyName,
        preferred_username = email,
        email,
        email_verified = true,
        updated_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    });
})
.WithName("UserInfo")
.Produces(200)
.Produces(401);

app.MapPost("/v1/token", async (HttpContext context, IMockTokenGenerator tokenGenerator, CancellationToken cancellationToken) =>
{
    IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);
    string issuer = GetIssuer(context.Request);
    string audience = ResolveClientId(context.Request, form);

    string grantType = form["grant_type"].ToString();
    string? scope = form["scope"].ToString();
    string username = form["username"].ToString();
    string password = form["password"].ToString();

    ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "Legacy token endpoint called. TraceId={TraceId} CorrelationId={CorrelationId} GrantType={GrantType} ClientIdHash={ClientIdHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        grantType,
        HashForLog(audience));

    if (string.IsNullOrWhiteSpace(grantType))
    {
        logger.LogWarning(
            "Legacy token request rejected: grant_type missing. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request", error_description = "grant_type is required" });
    }

    if (grantType != "client_credentials" && grantType != "password")
    {
        logger.LogWarning(
            "Legacy token request rejected: unsupported grant type. TraceId={TraceId} CorrelationId={CorrelationId} GrantType={GrantType}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            grantType);
        return Results.BadRequest(new { error = "unsupported_grant_type", error_description = $"grant_type '{grantType}' is not supported" });
    }

    if (grantType == "password" && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
    {
        logger.LogWarning(
            "Legacy token request rejected: password grant missing credentials. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request", error_description = "username and password are required for password grant" });
    }

    string token = tokenGenerator.GenerateToken(issuer, audience, grantType, scope, username, password);
    logger.LogInformation(
        "Legacy token issued. TraceId={TraceId} CorrelationId={CorrelationId} GrantType={GrantType} Scope={Scope}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        grantType,
        scope);

    return Results.Ok(new
    {
        access_token = token,
        token_type = "Bearer",
        expires_in = 3600,
        scope = scope ?? "openid email profile"
    });
})
.WithName("GetToken")
.Produces(200)
.Produces(400);

app.MapGet("/oauth2/v1/authorize", (HttpContext context, ILogger<Program> logger) =>
{
    IQueryCollection query = context.Request.Query;
    string clientId = query["client_id"].ToString();
    string redirectUri = query["redirect_uri"].ToString();
    string state = query["state"].ToString();
    string responseType = query["response_type"].ToString();
    string scope = query["scope"].ToString();
    string codeChallenge = query["code_challenge"].ToString();
    string codeChallengeMethod = query["code_challenge_method"].ToString();
    string nonce = query["nonce"].ToString();
    string loginHint = query["login_hint"].ToString();
    // Standard OAuth2 audience/resource parameter (used by Auth0, Okta, and this mock)
    string audience = query["audience"].ToString();
    if (string.IsNullOrWhiteSpace(audience))
    {
        audience = query["resource"].ToString();
    }

    logger.LogInformation(
        "Authorize request received. TraceId={TraceId} CorrelationId={CorrelationId} ClientIdHash={ClientIdHash} HasState={HasState} HasCodeChallenge={HasCodeChallenge} ChallengeMethod={ChallengeMethod}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        HashForLog(clientId),
        !string.IsNullOrWhiteSpace(state),
        !string.IsNullOrWhiteSpace(codeChallenge),
        codeChallengeMethod);

    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(responseType))
    {
        logger.LogWarning(
            "Authorize request rejected: invalid_request. TraceId={TraceId} CorrelationId={CorrelationId} MissingRequiredParams=true",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request" });
    }

    if (responseType != "code")
    {
        logger.LogWarning(
            "Authorize request rejected: unsupported_response_type. TraceId={TraceId} CorrelationId={CorrelationId} ResponseType={ResponseType}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            responseType);
        return Results.BadRequest(new { error = "unsupported_response_type" });
    }

    if (string.IsNullOrWhiteSpace(codeChallenge))
    {
        logger.LogWarning(
            "Authorize request rejected: missing code_challenge. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request", error_description = "code_challenge is required" });
    }

    if (string.IsNullOrWhiteSpace(codeChallengeMethod))
    {
        codeChallengeMethod = "S256";
    }

    string? interactionId = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
    pendingInteractions[interactionId] = new(
        clientId,
        redirectUri,
        state,
        scope,
        codeChallenge,
        codeChallengeMethod,
        nonce,
        loginHint,
        null,
        DateTimeOffset.UtcNow,
        string.IsNullOrWhiteSpace(audience) ? null : audience);

    logger.LogInformation(
        "Authorization interaction started. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash} ClientIdHash={ClientIdHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        HashForLog(interactionId),
        HashForLog(clientId));

    return RenderLoginPage(interactionId, loginHint, null);
})
.WithName("Authorize")
.Produces(302)
.Produces(400);

app.MapPost("/oauth2/v1/login", async (HttpContext context, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);
    string interactionId = form["interaction_id"].ToString();
    string username = form["username"].ToString();
    string password = form["password"].ToString();

    if (string.IsNullOrWhiteSpace(interactionId) ||
        !pendingInteractions.TryGetValue(interactionId, out PendingInteractionRecord? interaction))
    {
        logger.LogWarning(
            "Login rejected: unknown interaction. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId));
        return Results.BadRequest("Invalid or expired login session. Restart sign-in from your application.");
    }

    if (DateTimeOffset.UtcNow > interaction.IssuedAtUtc.Add(interactionLifetime))
    {
        pendingInteractions.TryRemove(interactionId, out _);
        logger.LogWarning(
            "Login rejected: interaction expired. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId));
        return Results.BadRequest("Login session expired. Restart sign-in from your application.");
    }

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning(
            "Login rejected: missing credentials. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId));
        return RenderLoginPage(interactionId, interaction.LoginHint, "Username and password are required.");
    }

    pendingInteractions[interactionId] = interaction with
    {
        Subject = username,
        IssuedAtUtc = DateTimeOffset.UtcNow
    };

    logger.LogInformation(
        "Primary authentication succeeded. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash} SubjectHash={SubjectHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        HashForLog(interactionId),
        HashForLog(username));

    if (enable2fa)
    {
        return RenderTwoFactorPage(interactionId, username, null);
    }

    // 2FA disabled — issue auth code directly
    pendingInteractions.TryRemove(interactionId, out _);

    string? authCode = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    authCodes[authCode] = new(
        interaction.ClientId,
        interaction.RedirectUri,
        interaction.Scope,
        interaction.CodeChallenge,
        interaction.CodeChallengeMethod,
        username,
        interaction.Nonce,
        DateTimeOffset.UtcNow,
        interaction.Audience);

    logger.LogInformation(
        "2FA skipped, authorization code issued. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash} AuthCodeHash={AuthCodeHash} SubjectHash={SubjectHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        HashForLog(interactionId),
        HashForLog(authCode),
        HashForLog(username));

    string redirectUrl = $"{interaction.RedirectUri}?code={authCode}";
    if (!string.IsNullOrWhiteSpace(interaction.State))
    {
        redirectUrl += $"&state={Uri.EscapeDataString(interaction.State)}";
    }

    return Results.Redirect(redirectUrl);
})
.WithName("Login")
.Produces(200)
.Produces(400);

app.MapPost("/oauth2/v1/verify-2fa", async (HttpContext context, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);
    string interactionId = form["interaction_id"].ToString();
    string verificationCode = form["verification_code"].ToString();

    if (string.IsNullOrWhiteSpace(interactionId) ||
        !pendingInteractions.TryGetValue(interactionId, out PendingInteractionRecord? interaction))
    {
        logger.LogWarning(
            "2FA rejected: unknown interaction. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId));
        return Results.BadRequest("Invalid or expired 2FA session. Restart sign-in from your application.");
    }

    if (DateTimeOffset.UtcNow > interaction.IssuedAtUtc.Add(interactionLifetime))
    {
        pendingInteractions.TryRemove(interactionId, out _);
        logger.LogWarning(
            "2FA rejected: interaction expired. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId));
        return Results.BadRequest("2FA session expired. Restart sign-in from your application.");
    }

    if (string.IsNullOrWhiteSpace(interaction.Subject))
    {
        logger.LogWarning(
            "2FA rejected: missing primary auth subject. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId));
        return RenderLoginPage(interactionId, interaction.LoginHint, "Please enter username and password first.");
    }

    if (!string.Equals(verificationCode, "123456", StringComparison.Ordinal))
    {
        logger.LogWarning(
            "2FA rejected: invalid code. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash} SubjectHash={SubjectHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(interactionId),
            HashForLog(interaction.Subject));
        return RenderTwoFactorPage(interactionId, interaction.Subject, "Invalid verification code.");
    }

    pendingInteractions.TryRemove(interactionId, out _);

    string? authCode = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    authCodes[authCode] = new(
        interaction.ClientId,
        interaction.RedirectUri,
        interaction.Scope,
        interaction.CodeChallenge,
        interaction.CodeChallengeMethod,
        interaction.Subject,
        interaction.Nonce,
        DateTimeOffset.UtcNow,
        interaction.Audience);

    logger.LogInformation(
        "2FA succeeded and authorization code issued. TraceId={TraceId} CorrelationId={CorrelationId} InteractionIdHash={InteractionIdHash} AuthCodeHash={AuthCodeHash} SubjectHash={SubjectHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        HashForLog(interactionId),
        HashForLog(authCode),
        HashForLog(interaction.Subject));

    string redirectUrl = $"{interaction.RedirectUri}?code={authCode}";
    if (!string.IsNullOrWhiteSpace(interaction.State))
    {
        redirectUrl += $"&state={Uri.EscapeDataString(interaction.State)}";
    }

    return Results.Redirect(redirectUrl);
})
.WithName("VerifyTwoFactor")
.Produces(302)
.Produces(400);

app.MapPost("/oauth2/v1/token", async (HttpContext context, IMockTokenGenerator tokenGenerator, CancellationToken cancellationToken) =>
{
    IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);
    string grantType = form["grant_type"].ToString();
    string issuer = GetIssuer(context.Request);
    ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    logger.LogInformation(
        "Token endpoint called. TraceId={TraceId} CorrelationId={CorrelationId} GrantType={GrantType}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        grantType);

    if (string.IsNullOrWhiteSpace(grantType))
    {
        logger.LogWarning(
            "Token request rejected: grant_type missing. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request", error_description = "grant_type is required" });
    }

    if (grantType == "authorization_code")
    {
        string code = form["code"].ToString();
        string redirectUri = form["redirect_uri"].ToString();
        string clientId = form["client_id"].ToString();
        string codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(codeVerifier))
        {
            logger.LogWarning(
                "Token exchange rejected: required fields missing. TraceId={TraceId} CorrelationId={CorrelationId}",
                context.TraceIdentifier,
                GetCorrelationId(context));
            return Results.BadRequest(new { error = "invalid_request", error_description = "code, redirect_uri, client_id, and code_verifier are required" });
        }

        if (!authCodes.TryRemove(code, out AuthCodeRecord? record))
        {
            logger.LogWarning(
                "Token exchange rejected: invalid auth code. TraceId={TraceId} CorrelationId={CorrelationId} AuthCodeHash={AuthCodeHash}",
                context.TraceIdentifier,
                GetCorrelationId(context),
                HashForLog(code));
            return Results.BadRequest(new { error = "invalid_grant", error_description = "authorization code is invalid or expired" });
        }

        if (DateTimeOffset.UtcNow > record.IssuedAtUtc.Add(authCodeLifetime))
        {
            logger.LogWarning(
                "Token exchange rejected: authorization code expired. TraceId={TraceId} CorrelationId={CorrelationId} AuthCodeHash={AuthCodeHash} IssuedAtUtc={IssuedAtUtc}",
                context.TraceIdentifier,
                GetCorrelationId(context),
                HashForLog(code),
                record.IssuedAtUtc);
            return Results.BadRequest(new { error = "invalid_grant", error_description = "authorization code is invalid or expired" });
        }

        if (!string.Equals(record.ClientId, clientId, StringComparison.Ordinal) ||
            !string.Equals(record.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Token exchange rejected: client or redirect mismatch. TraceId={TraceId} CorrelationId={CorrelationId} AuthCodeHash={AuthCodeHash}",
                context.TraceIdentifier,
                GetCorrelationId(context),
                HashForLog(code));
            return Results.BadRequest(new { error = "invalid_grant", error_description = "client_id or redirect_uri mismatch" });
        }

        string expectedChallenge = ComputeCodeChallenge(codeVerifier, record.CodeChallengeMethod);
        if (!string.Equals(expectedChallenge, record.CodeChallenge, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Token exchange rejected: PKCE mismatch. TraceId={TraceId} CorrelationId={CorrelationId} AuthCodeHash={AuthCodeHash} Method={Method}",
                context.TraceIdentifier,
                GetCorrelationId(context),
                HashForLog(code),
                record.CodeChallengeMethod);
            return Results.BadRequest(new { error = "invalid_grant", error_description = "code_verifier does not match code_challenge" });
        }

        string? userIdentifier = string.IsNullOrWhiteSpace(record.Subject) ? "devuser@example.com" : record.Subject;
        // Use the requested API audience for the access token; fall back to the client ID if no
        // audience was declared at the authorize step (preserves backward compatibility).
        string? accessTokenAudience = string.IsNullOrWhiteSpace(record.Audience) ? clientId : record.Audience;
        string accessToken = tokenGenerator.GenerateAccessToken(issuer, accessTokenAudience, record.Scope, userIdentifier, userIdentifier, userIdentifier);
        // ID token audience is always the relying-party client ID per the OIDC specification.
        string idToken = tokenGenerator.GenerateIdToken(issuer, clientId, userIdentifier, userIdentifier, userIdentifier, record.Nonce);

        logger.LogInformation(
            "Token exchange succeeded. TraceId={TraceId} CorrelationId={CorrelationId} ClientIdHash={ClientIdHash} AccessTokenAudienceHash={AccessTokenAudienceHash} SubjectHash={SubjectHash}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            HashForLog(clientId),
            HashForLog(accessTokenAudience),
            HashForLog(userIdentifier));

        return Results.Ok(new
        {
            token_type = "Bearer",
            expires_in = 3600,
            access_token = accessToken,
            id_token = idToken,
            scope = record.Scope ?? "openid email profile"
        });
    }

    if (grantType != "client_credentials" && grantType != "password")
    {
        logger.LogWarning(
            "Token request rejected: unsupported grant type. TraceId={TraceId} CorrelationId={CorrelationId} GrantType={GrantType}",
            context.TraceIdentifier,
            GetCorrelationId(context),
            grantType);
        return Results.BadRequest(new { error = "unsupported_grant_type", error_description = $"grant_type '{grantType}' is not supported" });
    }

    string? scope = form["scope"].ToString();
    string username = form["username"].ToString();
    string password = form["password"].ToString();

    if (grantType == "password" && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
    {
        logger.LogWarning(
            "Token request rejected: password grant missing credentials. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request", error_description = "username and password are required for password grant" });
    }

    string audience = ResolveClientId(context.Request, form);
    string token = tokenGenerator.GenerateToken(issuer, audience, grantType, scope, username, password);

    logger.LogInformation(
        "Token issued. TraceId={TraceId} CorrelationId={CorrelationId} GrantType={GrantType} ClientIdHash={ClientIdHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        grantType,
        HashForLog(audience));

    return Results.Ok(new
    {
        access_token = token,
        token_type = "Bearer",
        expires_in = 3600,
        scope = scope ?? "openid email profile"
    });
})
.WithName("Token")
.Produces(200)
.Produces(400);

app.MapPost("/oauth2/v1/introspect", async (HttpContext context, IMockTokenGenerator tokenGenerator, CancellationToken cancellationToken) =>
{
    IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);

    string token = form["token"].ToString();
    string clientId = form["client_id"].ToString();
    ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    if (string.IsNullOrWhiteSpace(token))
    {
        logger.LogWarning(
            "Introspection rejected: token missing. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.BadRequest(new { error = "invalid_request" });
    }

    string issuer = GetIssuer(context.Request);
    string audience = string.IsNullOrWhiteSpace(clientId) ? "mock-client-id" : clientId;
    bool isValid = tokenGenerator.ValidateToken(token, issuer, audience);

    logger.LogInformation(
        "Introspection completed. TraceId={TraceId} CorrelationId={CorrelationId} Active={Active} ClientIdHash={ClientIdHash}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        isValid,
        HashForLog(audience));

    return Results.Ok(new
    {
        active = isValid,
        token_type = "Bearer",
        scope = "openid email profile",
        sub = "mock-user-id",
        iss = issuer,
        aud = audience
    });
})
.WithName("Introspect")
.Produces(200)
.Produces(400);

app.MapMethods("/oauth2/v1/logout", ["GET", "POST"], (HttpContext context, ILogger<Program> logger) =>
{
    string postLogoutRedirectUri = context.Request.Query["post_logout_redirect_uri"].ToString();
    string state = context.Request.Query["state"].ToString();

    logger.LogInformation(
        "Logout endpoint called. TraceId={TraceId} CorrelationId={CorrelationId} HasPostLogoutRedirect={HasRedirect} HasState={HasState}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        !string.IsNullOrWhiteSpace(postLogoutRedirectUri),
        !string.IsNullOrWhiteSpace(state));

    if (string.IsNullOrWhiteSpace(postLogoutRedirectUri))
    {
        logger.LogInformation(
            "Logout completed without redirect. TraceId={TraceId} CorrelationId={CorrelationId}",
            context.TraceIdentifier,
            GetCorrelationId(context));
        return Results.NoContent();
    }

    if (!string.IsNullOrWhiteSpace(state))
    {
        char separator = postLogoutRedirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        postLogoutRedirectUri = $"{postLogoutRedirectUri}{separator}state={Uri.EscapeDataString(state)}";
    }

    logger.LogInformation(
        "Logout redirecting to client. TraceId={TraceId} CorrelationId={CorrelationId} RedirectUri={RedirectUri}",
        context.TraceIdentifier,
        GetCorrelationId(context),
        SanitizePathForLog(postLogoutRedirectUri));

    return Results.Redirect(postLogoutRedirectUri);
})
.WithName("EndSession")
.Produces(302)
.Produces(204);

app.Run();

static string GetCorrelationId(HttpContext httpContext)
{
    if (httpContext.Items.TryGetValue("CorrelationId", out object? correlationId) &&
        correlationId is string correlationIdString &&
        !string.IsNullOrWhiteSpace(correlationIdString))
    {
        return correlationIdString;
    }

    return Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}

static string HashForLog(string? input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return "(empty)";
    }

    byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hash[..6]);
}

static string SanitizePathForLog(string path)
{
    int querySeparatorIndex = path.IndexOf('?', StringComparison.Ordinal);
    return querySeparatorIndex >= 0 ? path[..querySeparatorIndex] : path;
}

static string GetIssuer(HttpRequest request)
{
    string? basePath = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
    return $"{request.Scheme}://{request.Host}{basePath}";
}

static string ComputeCodeChallenge(string codeVerifier, string codeChallengeMethod)
{
    if (string.Equals(codeChallengeMethod, "plain", StringComparison.OrdinalIgnoreCase))
    {
        return codeVerifier;
    }

    if (!string.Equals(codeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Unsupported code_challenge_method '{codeChallengeMethod}'.");
    }

    byte[] bytes = System.Text.Encoding.ASCII.GetBytes(codeVerifier);
    byte[] hash = SHA256.HashData(bytes);
    return Base64UrlEncoder.Encode(hash);
}

static string ResolveClientId(HttpRequest request, IFormCollection form)
{
    string formClientId = form["client_id"].ToString();
    if (!string.IsNullOrWhiteSpace(formClientId))
    {
        return formClientId;
    }

    string authorization = request.Headers.Authorization.ToString();
    if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        string encoded = authorization["Basic ".Length..].Trim();
        try
        {
            string decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            int separator = decoded.IndexOf(':');
            if (separator > 0)
            {
                string basicClientId = decoded[..separator];
                if (!string.IsNullOrWhiteSpace(basicClientId))
                {
                    return basicClientId;
                }
            }
        }
        catch (FormatException)
        {
        }
    }

    return "mock-client-id";
}

static IResult RenderLoginPage(string interactionId, string? suggestedUsername, string? error)
{
    string encodedInteractionId = WebUtility.HtmlEncode(interactionId);
    string encodedUsername = WebUtility.HtmlEncode(suggestedUsername ?? string.Empty);
    string errorMarkup = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<p style='color:#b42318;background:#fef3f2;padding:10px;border-radius:6px;border:1px solid #fecdca;'>{WebUtility.HtmlEncode(error)}</p>";

    string html = $$"""
                    <!doctype html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1" />
                        <title>Mock Okta - Sign In</title>
                        <style>
                            body { font-family: Segoe UI, Arial, sans-serif; background: #f4f6f8; margin: 0; }
                            .card { max-width: 420px; margin: 64px auto; padding: 24px; background: #fff; border-radius: 10px; box-shadow: 0 8px 30px rgba(0,0,0,0.08); }
                            h1 { margin-top: 0; font-size: 22px; }
                            label { display: block; margin: 12px 0 6px; font-weight: 600; }
                            input { width: 100%; box-sizing: border-box; padding: 10px; border: 1px solid #d0d5dd; border-radius: 6px; }
                            button { margin-top: 16px; width: 100%; background: #0f62fe; color: #fff; border: 0; padding: 11px; border-radius: 6px; font-weight: 600; cursor: pointer; }
                            .hint { color: #667085; font-size: 13px; }
                        </style>
                    </head>
                    <body>
                        <main class="card">
                            <h1>Sign in to Mock Okta</h1>
                            <p class="hint">This is a development-only mock sign-in page.</p>
                            {{errorMarkup}}
                            <form method="post" action="/oauth2/v1/login">
                                <input type="hidden" name="interaction_id" value="{{encodedInteractionId}}" />
                                <label for="username">Username</label>
                                <input id="username" name="username" autocomplete="username" value="{{encodedUsername}}" />
                                <label for="password">Password</label>
                                <input id="password" name="password" type="password" autocomplete="current-password" />
                                <button type="submit">Continue</button>
                            </form>
                        </main>
                    </body>
                    </html>
                    """;

    return Results.Content(html, "text/html");
}

static IResult RenderTwoFactorPage(string interactionId, string? username, string? error)
{
    string encodedInteractionId = WebUtility.HtmlEncode(interactionId);
    string encodedUsername = WebUtility.HtmlEncode(username ?? "user");
    string errorMarkup = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<p style='color:#b42318;background:#fef3f2;padding:10px;border-radius:6px;border:1px solid #fecdca;'>{WebUtility.HtmlEncode(error)}</p>";

    string html = $$"""
                    <!doctype html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1" />
                        <title>Mock Okta - Verify</title>
                        <style>
                            body { font-family: Segoe UI, Arial, sans-serif; background: #f4f6f8; margin: 0; }
                            .card { max-width: 420px; margin: 64px auto; padding: 24px; background: #fff; border-radius: 10px; box-shadow: 0 8px 30px rgba(0,0,0,0.08); }
                            h1 { margin-top: 0; font-size: 22px; }
                            label { display: block; margin: 12px 0 6px; font-weight: 600; }
                            input { width: 100%; box-sizing: border-box; padding: 10px; border: 1px solid #d0d5dd; border-radius: 6px; letter-spacing: 2px; }
                            button { margin-top: 16px; width: 100%; background: #0f62fe; color: #fff; border: 0; padding: 11px; border-radius: 6px; font-weight: 600; cursor: pointer; }
                            .hint { color: #667085; font-size: 13px; }
                            code { background: #f2f4f7; padding: 1px 5px; border-radius: 4px; }
                        </style>
                    </head>
                    <body>
                        <main class="card">
                            <h1>Two-Factor Verification</h1>
                            <p class="hint">Signed in as <strong>{{encodedUsername}}</strong>. Enter verification code <code>123456</code>.</p>
                            {{errorMarkup}}
                            <form method="post" action="/oauth2/v1/verify-2fa">
                                <input type="hidden" name="interaction_id" value="{{encodedInteractionId}}" />
                                <label for="verification_code">Verification code</label>
                                <input id="verification_code" name="verification_code" autocomplete="one-time-code" inputmode="numeric" maxlength="6" />
                                <button type="submit">Verify</button>
                            </form>
                        </main>
                    </body>
                    </html>
                    """;

    return Results.Content(html, "text/html");
}

record AuthCodeRecord(
    string ClientId,
    string RedirectUri,
    string? Scope,
    string CodeChallenge,
    string CodeChallengeMethod,
    string? Subject,
    string? Nonce,
    DateTimeOffset IssuedAtUtc,
    string? Audience
);

record PendingInteractionRecord(
    string ClientId,
    string RedirectUri,
    string? State,
    string? Scope,
    string CodeChallenge,
    string CodeChallengeMethod,
    string? Nonce,
    string? LoginHint,
    string? Subject,
    DateTimeOffset IssuedAtUtc,
    string? Audience
);
