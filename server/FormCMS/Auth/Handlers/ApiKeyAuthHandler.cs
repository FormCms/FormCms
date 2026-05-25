using FormCMS.Auth.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace FormCMS.Auth.Handlers;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AuthConfig _authConfig;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AuthConfig authConfig
    )
        : base(options, logger, encoder)
    {
        _authConfig = authConfig;
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(_authConfig.KeyAuthConfig));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var authHeader = authHeaderValues.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header format. Expected: Bearer <api-key>"));
        }

        var providedApiKey = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        if (providedApiKey != _authConfig.KeyAuthConfig!.Key)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthorized client"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, KeyAuthConstants.ApiKeyUser),
            new Claim(ClaimTypes.Role, Roles.Sa)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}