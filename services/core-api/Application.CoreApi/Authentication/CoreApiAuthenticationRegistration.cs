using System.Text.Json;
using Application.IdentityAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Application.CoreApi.Authentication;

internal static class CoreApiAuthenticationRegistration
{
    internal static IServiceCollection AddCoreApiAuthentication(
        this IServiceCollection services,
        OidcAuthenticationConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration.Authority;
                options.Audience = configuration.Audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.BackchannelTimeout = configuration.BackchannelTimeout;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = configuration.Authority,
                    ValidAudience = configuration.Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = configuration.ClockSkew,
                    NameClaimType = "sub",
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var issuers = context.Principal?.FindAll("iss")
                            .Select(claim => claim.Value).ToArray() ?? [];
                        var subjects = context.Principal?.FindAll("sub")
                            .Select(claim => claim.Value).ToArray() ?? [];

                        if (issuers.Length != 1 || subjects.Length != 1)
                        {
                            context.Fail("The token does not contain the required issuer and subject identity.");
                            return Task.CompletedTask;
                        }

                        try
                        {
                            _ = ExternalIdentity.Create(issuers[0], subjects[0]);
                        }
                        catch (ArgumentException)
                        {
                            context.Fail("The token contains an invalid stable identity.");
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        await JsonSerializer.SerializeAsync(
                            context.Response.Body,
                            new
                            {
                                type = "about:blank",
                                title = "Authentication required.",
                                status = StatusCodes.Status401Unauthorized,
                                code = "authentication_required",
                            },
                            cancellationToken: context.HttpContext.RequestAborted);
                    },
                };
            });

        return services;
    }
}
