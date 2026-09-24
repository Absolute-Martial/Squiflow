using Autofac.Extensions.DependencyInjection;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Application.Branding;
using Application.CoreApi;
using Application.CoreApi.Authentication;
using Application.CoreApi.Authorization;
using Application.CoreApi.Composition;
using Application.CoreApi.Health;
using Application.Tenancy;

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
builder.Services.AddCoreApiAuthorization(openFgaAuthorizationConfiguration);
builder.Services.AddCoreApiPersistence(databaseConfiguration);
builder.Services.AddCoreApiAuthentication(authenticationConfiguration);
builder.Services
    .AddMultiTenant<TenantInfo>()
    .WithRouteStrategy("tenantId", useTenantAmbientRouteValue: false)
    .WithEchoStore();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddCheck<PrimaryDatabaseReadinessCheck>(
        "primary_database", tags: ["readiness"], timeout: TimeSpan.FromSeconds(5))
    .AddCheck<OpenFgaReadinessCheck>(
        "openfga", tags: ["readiness"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<ReadinessStatusCache>();
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

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders", TenantOrderEndpoint.CreateAsync)
    .WithName("CreateTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Creates a tenant order draft using a required Idempotency-Key header.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_create_order permission.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderCreation))
    .WithMetadata(new RequestSizeLimitAttribute(TenantOrderEndpoint.MaximumCreateRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<CreateOrderDraftPayload>("application/json")
    .Produces<OrderDraftResponse>(StatusCodes.Status201Created)
    .Produces<OrderDraftResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/v1/tenants/{tenantId:guid}/orders", TenantOrderEndpoint.ListAsync)
    .WithName("ListTenantOrderDrafts")
    .WithTags("Orders")
    .WithSummary("Returns a bounded page of tenant order drafts after current membership and OpenFGA permission checks.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_view_orders permission.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderBrowse))
    .RequireAuthorization()
    .Produces<OrderDraftPageResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}", TenantOrderEndpoint.GetAsync)
    .WithName("GetTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Returns a tenant order draft after current membership and OpenFGA permission checks.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_view_orders permission.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderRead))
    .RequireAuthorization()
    .Produces<OrderDraftResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/abandon", TenantOrderEndpoint.AbandonAsync)
    .WithName("AbandonTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Abandons a tenant order draft using an expected revision and Idempotency-Key.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_abandon_order permission. Supply JSON {\"expectedRevision\":1} and one Idempotency-Key header; an exact retry returns the committed result.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderAbandon))
    .WithMetadata(new RequestSizeLimitAttribute(TenantOrderEndpoint.MaximumAbandonRequestBodyBytes))
    .RequireAuthorization()
    .Produces<AbandonOrderDraftResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
})
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicLiveness));

app.MapGet("/health/ready", async (ReadinessStatusCache cache, HttpContext context) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return await cache.IsReadyAsync(context.RequestAborted)
            ? Results.StatusCode(StatusCodes.Status200OK)
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    })
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicReadiness))
    .ExcludeFromDescription();

await app.RunAsync();

public partial class Program;
