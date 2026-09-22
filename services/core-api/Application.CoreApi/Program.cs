using Autofac.Extensions.DependencyInjection;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text.Json;
using Application.Branding;
using Application.CoreApi;
using Application.CoreApi.Authorization;
using Application.CoreApi.Composition;
using Application.IdentityAccess;
using Application.IdentityAccess.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

var brandingConfiguration = builder.Configuration
    .GetRequiredSection(BrandingConfiguration.SectionName)
    .Get<BrandingConfiguration>()
    ?? throw new InvalidOperationException("The Branding configuration section is required.");
var authenticationConfiguration = OidcAuthenticationConfiguration.From(builder.Configuration);
var openFgaAuthorizationConfiguration = OpenFgaAuthorizationConfiguration.From(builder.Configuration);
var databaseConfiguration = RuntimeDatabaseConfiguration.From(builder.Configuration);
RequestHostConfiguration.Validate(builder.Configuration);
var brandProfile = brandingConfiguration.ToProfile();
var bootstrapCacheMaxAgeSeconds = brandingConfiguration.GetCacheMaxAgeSeconds();

builder.Services.AddSingleton(brandProfile);
builder.Services.AddSingleton(authenticationConfiguration);
builder.Services.AddSingleton(openFgaAuthorizationConfiguration);
builder.Services.AddSingleton<OpenFga.Sdk.Client.IOpenFgaClient>(_ =>
    new OpenFga.Sdk.Client.OpenFgaClient(openFgaAuthorizationConfiguration.ToClientConfiguration()));
builder.Services.AddSingleton<ITenantWorkspaceAuthorization, OpenFgaTenantWorkspaceAuthorization>();
builder.Services.AddScoped<IAuthorizationHandler, ViewTenantWorkspaceAuthorizationHandler>();
builder.Services.AddSingleton(databaseConfiguration);
builder.Services.AddSingleton<NpgsqlDataSource>(serviceProvider =>
    databaseConfiguration.CreateDataSource(
        serviceProvider.GetRequiredService<ILoggerFactory>()));
builder.Services.AddDbContext<IdentityAccessDbContext>((serviceProvider, options) =>
    PostgresIdentityAccessOptions.Configure(
        options,
        serviceProvider.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddScoped<IAccountBindingDirectory, PostgresAccountBindingDirectory>();
builder.Services.AddScoped<ResolveAccountBinding>();
builder.Services.AddDbContext<TenancyDbContext>((serviceProvider, options) =>
    PostgresTenancyOptions.Configure(
        options,
        serviceProvider.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
builder.Services.AddScoped<ResolveTenantContext>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authenticationConfiguration.Authority;
        options.Audience = authenticationConfiguration.Audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.SaveToken = false;
        options.IncludeErrorDetails = false;
        options.BackchannelTimeout = authenticationConfiguration.BackchannelTimeout;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authenticationConfiguration.Authority,
            ValidAudience = authenticationConfiguration.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = authenticationConfiguration.ClockSkew,
            NameClaimType = "sub",
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var issuers = context.Principal?.FindAll("iss").Select(claim => claim.Value).ToArray() ?? [];
                var subjects = context.Principal?.FindAll("sub").Select(claim => claim.Value).ToArray() ?? [];

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
builder.Services.AddAuthorization();
builder.Services
    .AddMultiTenant<TenantInfo>()
    .WithRouteStrategy("tenantId", useTenantAmbientRouteValue: false)
    .WithEchoStore();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddCoreApiOpenApi();
builder.Services.AddProfileRuntimeComposition(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();
app.UseMultiTenant();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi("/openapi/{documentName}.json")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicApiDescription));

app.MapGet("/api/v1/application/bootstrap", (BrandProfile brand, HttpResponse response) =>
    {
        response.Headers.ETag = $"\"{brand.Revision}\"";
        response.Headers.CacheControl = $"public,max-age={bootstrapCacheMaxAgeSeconds}";
        return TypedResults.Ok(brand);
    })
    .WithName("GetApplicationBootstrap")
    .WithTags("Application")
    .WithSummary("Returns the public application identity used by presentation clients.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicApplicationBootstrap))
    .Produces<BrandProfile>();

app.MapGet("/api/v1/account", AuthenticatedAccountEndpoint.GetAsync)
    .WithName("GetAuthenticatedAccount")
    .WithTags("Account")
    .WithSummary("Resolves the validated external identity to its active application account.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthenticatedAccount))
    .RequireAuthorization()
    .Produces<AuthenticatedAccountResponse>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden);

app.MapGet("/api/v1/account/tenants", TenantMembershipEndpoint.ListAsync)
    .WithName("ListAuthenticatedAccountTenants")
    .WithTags("Account")
    .WithSummary("Lists current active tenant memberships for the active application account.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthenticatedTenantMemberships))
    .RequireAuthorization()
    .Produces<TenantMembershipResponse[]>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden);

app.MapGet("/api/v1/tenants/{tenantId:guid}/workspace", TenantWorkspaceEndpoint.GetAsync)
    .WithName("GetTenantWorkspace")
    .WithTags("Tenant")
    .WithSummary("Returns a tenant workspace after current membership and OpenFGA permission checks.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantWorkspace))
    .RequireAuthorization()
    .Produces<TenantWorkspaceResponse>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
})
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicLiveness));

await app.RunAsync();

public partial class Program;
