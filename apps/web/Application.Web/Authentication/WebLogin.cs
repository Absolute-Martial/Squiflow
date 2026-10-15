using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Application.Web.Authentication;

internal static class WebLogin
{
    internal static void Add(WebApplicationBuilder builder, WebLoginConfiguration? login)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WebSessionTickets>();
        if (login is not null) builder.Services.AddSingleton(login);
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, WebSessionRevalidation>();
        var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);
        builder.Services.AddAuthorization();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        auth.AddCookie(options =>
        {
            options.Cookie.Name = "__Host-Application.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.SlidingExpiration = false;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
            options.LoginPath = "/";
        });
        builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<WebSessionTickets>((options, tickets) => options.SessionStore = tickets);
        if (login is null) return;
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.OpenIdConnect", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        auth.AddOpenIdConnect(options =>
        {
            options.Authority = login.Authority.AbsoluteUri.TrimEnd('/');
            options.ClientId = login.ClientId;
            options.ClientSecret = login.ClientSecret;
            options.ResponseType = "code";
            options.ResponseMode = "query";
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.RequireHttpsMetadata = true;
            options.BackchannelTimeout = TimeSpan.FromSeconds(10);
            options.BackchannelHttpHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                MaxConnectionsPerServer = 2,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(3);
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add(login.ApiScope);
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.TokenValidationParameters.ValidIssuer = options.Authority;
            options.TokenValidationParameters.ValidAudience = login.ClientId;
            options.TokenValidationParameters.ValidAlgorithms = ["RS256"];
            options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            options.TokenValidationParameters.IncludeTokenOnFailedValidation = false;
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = new Uri(login.PublicOrigin, "/signin-oidc").AbsoluteUri;
                return Task.CompletedTask;
            };
            options.Events.OnTicketReceived = context =>
            {
                if (context.Properties is null || context.Principal is null)
                {
                    context.Fail("Invalid application session.");
                    return Task.CompletedTask;
                }
                var expires = context.Properties.GetTokenValue("expires_at");
                var token = context.Properties.GetTokenValue("access_token");
                if (string.IsNullOrEmpty(token) || token.Length > 16 * 1024 || token.Any(char.IsControl) || token.Any(char.IsWhiteSpace) ||
                    !DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var tokenExpiry) ||
                    tokenExpiry <= DateTimeOffset.UtcNow)
                {
                    context.Fail("Invalid application credential.");
                    return Task.CompletedTask;
                }
                context.Properties.IsPersistent = false;
                context.Properties.AllowRefresh = false;
                context.Properties.ExpiresUtc = tokenExpiry < DateTimeOffset.UtcNow.AddMinutes(20) ? tokenExpiry : DateTimeOffset.UtcNow.AddMinutes(20);
                context.Properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);
                var claims = context.Principal!.Claims.Where(claim => claim.Type is "iss" or "sub").ToList();
                claims.Add(new Claim(WebSessionTickets.SessionClaim, WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))));
                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApplicationSession"));
                return Task.CompletedTask;
            };
            options.Events.OnTokenResponseReceived = context =>
            {
                if (context.TokenEndpointResponse.IdToken?.Length > 16 * 1024)
                    context.Fail("Invalid application identity response.");
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/login-result?status=failed");
                return Task.CompletedTask;
            };
        });
        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
            options => options.Backchannel.MaxResponseContentBufferSize = 128 * 1024);
    }

    internal static void Map(WebApplication app, WebLoginConfiguration? login)
    {
        app.MapPost("/auth/login", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (login is null) return Results.StatusCode(503);
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = "/workspace" });
            return Results.Empty;
        });
        app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login-result?status=signed-out");
        });
    }
}
