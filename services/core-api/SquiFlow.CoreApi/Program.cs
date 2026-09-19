using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using SquiFlow.Branding;
using SquiFlow.CoreApi;
using SquiFlow.CoreApi.Composition;
using SquiFlow.IdentityAccess;
using SquiFlow.IdentityAccess.Postgres;
using SquiFlow.Tenancy;
using SquiFlow.Tenancy.Postgres;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

var brandingConfiguration = builder.Configuration
    .GetRequiredSection(BrandingConfiguration.SectionName)
    .Get<BrandingConfiguration>()
    ?? throw new InvalidOperationException("The Branding configuration section is required.");
var authenticationConfiguration = OidcAuthenticationConfiguration.From(builder.Configuration);
var connectionString = builder.Configuration.GetConnectionString("PrimaryDatabase");
ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

builder.Services.AddSingleton(brandingConfiguration.ToProfile());
builder.Services.AddSingleton(authenticationConfiguration);
builder.Services.AddDbContext<IdentityAccessDbContext>(options =>
    PostgresIdentityAccessOptions.Configure(options, connectionString));
builder.Services.AddScoped<IAccountBindingDirectory, PostgresAccountBindingDirectory>();
builder.Services.AddScoped<ResolveAccountBinding>();
builder.Services.AddDbContext<TenancyDbContext>(options =>
    PostgresTenancyOptions.Configure(options, connectionString));
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
        options.BackchannelTimeout = TimeSpan.FromSeconds(10);
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
            ClockSkew = TimeSpan.FromMinutes(1),
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
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddCoreApiOpenApi();
builder.Services.AddProfileRuntimeComposition(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi("/openapi/{documentName}.json")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicApiDescription));

app.MapGet("/api/v1/application/bootstrap", (BrandProfile brand, HttpResponse response) =>
    {
        response.Headers.ETag = $"\"{brand.Revision}\"";
        response.Headers.CacheControl = "public,max-age=300";
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

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
})
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicLiveness));

await app.RunAsync();

public partial class Program;
