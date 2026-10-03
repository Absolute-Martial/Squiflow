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

var brandingSection = builder.Configuration.GetRequiredSection(BrandingConfiguration.SectionName);
var brandingConfiguration = brandingSection
    .Get<BrandingConfiguration>()
    ?? throw new InvalidOperationException("The Branding configuration section is required.");
var bootstrapCacheConfiguration = brandingSection
    .Get<BootstrapCacheConfiguration>()
    ?? throw new InvalidOperationException("The Branding bootstrap cache policy is required.");
var authenticationConfiguration = OidcAuthenticationConfiguration.From(builder.Configuration);
var openFgaAuthorizationConfiguration = OpenFgaAuthorizationConfiguration.From(builder.Configuration);
var databaseConfiguration = RuntimeDatabaseConfiguration.From(builder.Configuration);
RequestHostConfiguration.Validate(builder.Configuration);
var brandProfile = brandingConfiguration.ToProfile();
var bootstrapCacheMaxAgeSeconds = bootstrapCacheConfiguration.GetCacheMaxAgeSeconds();

builder.Services.AddSingleton(brandProfile);
builder.Services.AddCoreApiAuthorization(openFgaAuthorizationConfiguration);
builder.Services.AddCoreApiPersistence(databaseConfiguration);
builder.Services.AddCoreApiAuthentication(authenticationConfiguration);
builder.Services.AddCoreApiAdmission(builder.Configuration);
builder.Services.AddCoreApiRequestBudgets(builder.Configuration);
builder.Services.AddSingleton<CoreApiMutationDiagnostics>();
builder.Services
    .AddMultiTenant<TenantInfo>()
    .WithRouteStrategy("tenantId", useTenantAmbientRouteValue: false)
    .WithEchoStore();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<CoreApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<PrimaryDatabaseReadinessCheck>(
        "primary_database", tags: ["readiness"], timeout: TimeSpan.FromSeconds(5))
    .AddCheck<OpenFgaReadinessCheck>(
        "openfga", tags: ["readiness"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<ReadinessStatusCache>();
builder.Services.AddCoreApiOpenApi();
builder.Services.AddProfileRuntimeComposition(builder.Configuration);

var app = builder.Build();

// Handled failures emit only the safe event owned by CoreApiExceptionHandler.
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseRouting();
app.UseCoreApiNoStoreHeaders();
app.UseCoreApiRequestBudgets();
app.UseRateLimiter();
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
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/v1/account/tenants", TenantMembershipEndpoint.ListAsync)
    .WithName("ListAuthenticatedAccountTenants")
    .WithTags("Account")
    .WithSummary("Lists current active tenant memberships for the active application account.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthenticatedTenantMemberships))
    .RequireAuthorization()
    .Produces<TenantMembershipResponse[]>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/v1/tenants/{tenantId:guid}/workspace", TenantWorkspaceEndpoint.GetAsync)
    .WithName("GetTenantWorkspace")
    .WithTags("Tenant")
    .WithSummary("Returns a tenant workspace after current membership and OpenFGA permission checks.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantWorkspace))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<TenantWorkspaceResponse>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

var organizations = "/api/v1/tenants/{tenantId:guid}/customers/organizations";
app.MapPost(organizations, TenantCustomerEndpoint.CreateOrganizationAsync)
    .WithName("CreateCustomerOrganization").WithTags("Customers")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedCustomerOrganizationCreation))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
    .RequireAuthorization().Accepts<TenantCustomerEndpoint.NamePayload>("application/json")
    .Produces<Application.Customers.CustomerOrganizationSnapshot>(201)
    .Produces<Application.Customers.CustomerOrganizationSnapshot>(200)
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(413).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(organizations, TenantCustomerEndpoint.ListOrganizationsAsync)
    .WithName("ListCustomerOrganizations").WithTags("Customers")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedCustomerOrganizationBrowse))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<CustomerOrganizationPageResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(organizations + "/{organizationId:guid}", TenantCustomerEndpoint.GetOrganizationAsync)
    .WithName("GetCustomerOrganization").WithTags("Customers")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedCustomerOrganizationRead))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<Application.Customers.CustomerOrganizationSnapshot>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
var programs = organizations + "/{organizationId:guid}/programs";
app.MapPost(programs, TenantCustomerEndpoint.CreateProgramAsync)
    .WithName("CreateCustomerProgram").WithTags("Customers")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedCustomerProgramCreation))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
    .RequireAuthorization().Accepts<TenantCustomerEndpoint.NamePayload>("application/json")
    .Produces<Application.Customers.CustomerProgramSnapshot>(201)
    .Produces<Application.Customers.CustomerProgramSnapshot>(200)
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(programs, TenantCustomerEndpoint.ListProgramsAsync)
    .WithName("ListCustomerPrograms").WithTags("Customers")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedCustomerProgramBrowse))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<CustomerProgramPageResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(programs + "/{programId:guid}", TenantCustomerEndpoint.GetProgramAsync)
    .WithName("GetCustomerProgram").WithTags("Customers")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedCustomerProgramRead))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<Application.Customers.CustomerProgramSnapshot>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders", TenantOrderEndpoint.CreateAsync)
    .WithName("CreateTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Creates a tenant order draft using a required Idempotency-Key header.")
    .WithDescription("Requires current tenant membership and both pinned OpenFGA can_create_order and can_apply_manual_price permissions.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderCreation))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
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
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders/price-preview", TenantOrderPricePreviewEndpoint.PreviewAsync)
    .WithName("PreviewTenantOrderDraftPrice")
    .WithTags("Orders")
    .WithSummary("Calculates supplied draft selling prices without saving an order.")
    .WithDescription("Requires current tenant membership and both can_create_order and can_apply_manual_price. Returns line amounts rounded to four decimals using ToEven and their sum. This preview does not select or approve prices, create a quotation, reserve stock or commit a charge. No Idempotency-Key is required. Submit the full draft separately; creation validates it again.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderPricePreview))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantOrderPricePreviewEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<OrderDraftPricePreviewPayload>("application/json")
    .Produces<OrderDraftPricePreviewResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(413)
    .ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapGet("/api/v1/tenants/{tenantId:guid}/orders", TenantOrderEndpoint.ListAsync)
    .WithName("ListTenantOrderDrafts")
    .WithTags("Orders")
    .WithSummary("Returns a bounded page of tenant order drafts after current membership and OpenFGA permission checks.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_view_orders permission.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderBrowse))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<OrderDraftPageResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}", TenantOrderEndpoint.GetAsync)
    .WithName("GetTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Returns a tenant order draft after current membership and OpenFGA permission checks.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_view_orders permission.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderRead))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<OrderDraftResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/actions", TenantOrderActionsEndpoint.GetAsync)
    .WithName("GetTenantOrderDraftActions")
    .WithTags("Orders")
    .WithSummary("Explains current revision, abandonment, and commitment availability for the observed draft.")
    .WithDescription("Requires current membership and can_view_orders. Availability combines the observed lifecycle with current can_edit_order plus can_apply_manual_price for revision, can_abandon_order for abandonment, and can_commit_order for commitment. This read changes nothing and grants no command authority. Commands independently recheck permission, expectedRevision and Idempotency-Key; guidance may become stale.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderActions))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<OrderDraftActionsResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
    .ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapGet("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/history", TenantOrderHistoryEndpoint.GetAsync)
    .WithName("GetTenantOrderDraftHistory")
    .WithTags("Orders")
    .WithSummary("Returns retained priced snapshots and actors for successful draft commands.")
    .WithDescription("Requires current membership and can_view_orders. Newest revision first; limit defaults to 5 and cannot exceed 10. Use nextBeforeRevision to request older revisions. History reads immutable command receipts and performs no mutation. This is draft history, not invoice or settlement history.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderHistory))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<OrderDraftHistoryResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
    .ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPut("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/draft", TenantOrderEndpoint.ReviseAsync)
    .WithName("ReviseTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Replaces a tenant order draft using an expected revision and Idempotency-Key.")
    .WithDescription("Requires current tenant membership and both pinned OpenFGA can_edit_order and can_apply_manual_price permissions, including full replacements that retain the same prices. Supply the complete priced draft with expectedRevision; an exact retry returns the committed result.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderRevision))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantOrderEndpoint.MaximumReviseRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<ReviseOrderDraftPayload>("application/json")
    .Produces<OrderDraftResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/abandon", TenantOrderEndpoint.AbandonAsync)
    .WithName("AbandonTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Abandons a tenant order draft using an expected revision and Idempotency-Key.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_abandon_order permission. Supply JSON {\"expectedRevision\":1} and one Idempotency-Key header; an exact retry returns the committed result.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderAbandon))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantOrderEndpoint.MaximumAbandonRequestBodyBytes))
    .RequireAuthorization()
    .Produces<AbandonOrderDraftResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/commit", TenantOrderCommitEndpoint.CommitAsync)
    .WithName("CommitTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Commits the current priced order draft using an expected revision and Idempotency-Key.")
    .WithDescription("Requires current tenant membership and the pinned OpenFGA can_commit_order permission, including for retries. Supply JSON {\"expectedRevision\":1} and one Idempotency-Key header. Returns only commitment metadata; commitment does not bill, reserve stock or trigger fulfillment.")
    .WithMetadata(new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantOrderCommit))
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantOrderEndpoint.MaximumAbandonRequestBodyBytes))
    .RequireAuthorization()
    .Produces<CommitOrderDraftResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

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

app.ValidateCoreApiEndpointAccess();
await app.RunAsync();

public partial class Program;
