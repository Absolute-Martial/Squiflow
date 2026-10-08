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
using Application.CoreApi.ImportExecution;
using Application.CoreApi.Storage;
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
var highRiskActionConfiguration = HighRiskActionAdmissionConfiguration.From(builder.Configuration);
var databaseConfiguration = RuntimeDatabaseConfiguration.From(builder.Configuration);
var objectStorageConfiguration = HuggingFaceObjectStoreConfiguration.From(builder.Configuration);
RequestHostConfiguration.Validate(builder.Configuration);
var brandProfile = brandingConfiguration.ToProfile();
var bootstrapCacheMaxAgeSeconds = bootstrapCacheConfiguration.GetCacheMaxAgeSeconds();

builder.Services.AddSingleton(brandProfile);
builder.Services.AddCoreApiAuthorization(openFgaAuthorizationConfiguration);
builder.Services.AddCoreApiPersistence(databaseConfiguration);
builder.Services.AddObjectStorage(objectStorageConfiguration);
builder.Services.AddCustomerImportExecution(builder.Configuration, databaseConfiguration);
builder.Services.AddCoreApiAuthentication(authenticationConfiguration);
builder.Services.AddCoreApiAdmission(builder.Configuration);
builder.Services.AddCoreApiRequestBudgets(builder.Configuration);
builder.Services.AddSingleton(highRiskActionConfiguration);
builder.Services.AddSingleton<HighRiskActionAdmission>();
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
app.UseCoreApiApplicationAuthorization();

app.MapOpenApi("/openapi/{documentName}.json")
    .WithCoreApiAccess(EndpointAccess.PublicApiDescription);

app.MapGet("/api/v1/application/bootstrap", (BrandProfile brand, HttpResponse response) =>
    {
        response.Headers.ETag = $"\"{brand.Revision}\"";
        response.Headers.CacheControl = $"public,max-age={bootstrapCacheMaxAgeSeconds}";
        return TypedResults.Ok(brand);
    })
    .WithName("GetApplicationBootstrap")
    .WithTags("Application")
    .WithSummary("Returns the public application identity used by presentation clients.")
    .WithCoreApiAccess(EndpointAccess.PublicApplicationBootstrap)
    .Produces<BrandProfile>();

app.MapGet("/api/v1/account", AuthenticatedAccountEndpoint.GetAsync)
    .WithName("GetAuthenticatedAccount")
    .WithTags("Account")
    .WithSummary("Resolves the validated external identity to its active application account.")
    .WithCoreApiAccess(EndpointAccess.AuthenticatedAccount)
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
    .WithCoreApiAccess(EndpointAccess.AuthenticatedTenantMemberships)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantWorkspace)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantWorkspace)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<TenantWorkspaceResponse>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

var authorizationBase = "/api/v1/tenants/{tenantId:guid}/authorization";
app.MapGet(authorizationBase, TenantAuthorizationAdministrationEndpoint.GetAsync)
    .WithName("GetTenantAuthorizationAdministration")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<TenantAuthorizationAdministrationResponse>()
    .ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/permissions/grant", TenantAuthorizationAdministrationEndpoint.GrantPermissionAsync)
    .WithName("ProposeTenantPermissionGrant")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.PermissionChangePayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/permissions/revoke", TenantAuthorizationAdministrationEndpoint.RevokePermissionAsync)
    .WithName("ProposeTenantPermissionRevocation")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.PermissionChangePayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapGet($"{authorizationBase}/proposals/{{proposalId:guid}}", TenantAuthorizationAdministrationEndpoint.GetProposalAsync)
    .WithName("GetTenantAuthorizationProposal")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<TenantAuthorizationProposal>()
    .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/proposals/{{proposalId:guid}}/reconcile", TenantAuthorizationAdministrationEndpoint.ReconcileProposalAsync)
    .WithName("ReconcileTenantAuthorizationProposal")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization()
    .Produces<TenantAuthorizationProposal>()
    .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/roles", TenantAuthorizationAdministrationEndpoint.CreateRoleAsync)
    .WithName("ProposeTenantCustomRoleCreation")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.CreateRolePayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPut($"{authorizationBase}/roles/{{roleId:guid}}", TenantAuthorizationAdministrationEndpoint.ReviseRoleAsync)
    .WithName("ProposeTenantCustomRoleRevision")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.ReviseRolePayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/roles/{{roleId:guid}}/retire", TenantAuthorizationAdministrationEndpoint.RetireRoleAsync)
    .WithName("ProposeTenantCustomRoleRetirement")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.RetireRolePayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/roles/{{roleId:guid}}/assignments/{{accountId:guid}}/assign", TenantAuthorizationAdministrationEndpoint.AssignRoleAsync)
    .WithName("ProposeTenantCustomRoleAssignment")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.RoleAssignmentPayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/roles/{{roleId:guid}}/assignments/{{accountId:guid}}/remove", TenantAuthorizationAdministrationEndpoint.UnassignRoleAsync)
    .WithName("ProposeTenantCustomRoleUnassignment")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.RoleAssignmentPayload>("application/json")
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost($"{authorizationBase}/owner-transfer", TenantAuthorizationAdministrationEndpoint.TransferOwnerAsync)
    .WithName("TransferTenantInitialOwner")
    .WithTags("Authorization")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantRoleAdministration)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantRoleAdministration)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantAuthorizationAdministrationEndpoint.MaximumRequestBodyBytes))
    .RequireAuthorization()
    .Accepts<TenantAuthorizationAdministrationEndpoint.OwnerTransferPayload>("application/json")
    .Produces<TenantOwnerTransferResult>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

var organizations = "/api/v1/tenants/{tenantId:guid}/customers/organizations";
app.MapPost(organizations, TenantCustomerEndpoint.CreateOrganizationAsync)
    .WithName("CreateCustomerOrganization").WithTags("Customers")
    .WithCoreApiAccess(EndpointAccess.AuthorizedCustomerOrganizationCreation)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedCustomerOrganizationCreation)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
    .RequireAuthorization().Accepts<TenantCustomerEndpoint.NamePayload>("application/json")
    .Produces<Application.Customers.CustomerOrganizationSnapshot>(201)
    .Produces<Application.Customers.CustomerOrganizationSnapshot>(200)
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(413).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(organizations, TenantCustomerEndpoint.ListOrganizationsAsync)
    .WithName("ListCustomerOrganizations").WithTags("Customers")
    .WithCoreApiAccess(EndpointAccess.AuthorizedCustomerOrganizationBrowse)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedCustomerOrganizationBrowse)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<CustomerOrganizationPageResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(organizations + "/{organizationId:guid}", TenantCustomerEndpoint.GetOrganizationAsync)
    .WithName("GetCustomerOrganization").WithTags("Customers")
    .WithCoreApiAccess(EndpointAccess.AuthorizedCustomerOrganizationRead)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedCustomerOrganizationRead)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<Application.Customers.CustomerOrganizationSnapshot>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
var programs = organizations + "/{organizationId:guid}/programs";
app.MapPost(programs, TenantCustomerEndpoint.CreateProgramAsync)
    .WithName("CreateCustomerProgram").WithTags("Customers")
    .WithCoreApiAccess(EndpointAccess.AuthorizedCustomerProgramCreation)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedCustomerProgramCreation)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
    .RequireAuthorization().Accepts<TenantCustomerEndpoint.NamePayload>("application/json")
    .Produces<Application.Customers.CustomerProgramSnapshot>(201)
    .Produces<Application.Customers.CustomerProgramSnapshot>(200)
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(programs, TenantCustomerEndpoint.ListProgramsAsync)
    .WithName("ListCustomerPrograms").WithTags("Customers")
    .WithCoreApiAccess(EndpointAccess.AuthorizedCustomerProgramBrowse)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedCustomerProgramBrowse)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<CustomerProgramPageResponse>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
app.MapGet(programs + "/{programId:guid}", TenantCustomerEndpoint.GetProgramAsync)
    .WithName("GetCustomerProgram").WithTags("Customers")
    .WithCoreApiAccess(EndpointAccess.AuthorizedCustomerProgramRead)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedCustomerProgramRead)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .RequireAuthorization().Produces<Application.Customers.CustomerProgramSnapshot>()
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);

app.MapPost("/api/v1/tenants/{tenantId:guid}/orders", TenantOrderEndpoint.CreateAsync)
    .WithName("CreateTenantOrderDraft")
    .WithTags("Orders")
    .WithSummary("Creates a tenant order draft using a required Idempotency-Key header.")
    .WithDescription("Requires current tenant membership and both pinned OpenFGA can_create_order and can_apply_manual_price permissions.")
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderCreation)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderCreation)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderPricePreview)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderPricePreview)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderBrowse)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderBrowse)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderRead)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderRead)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderActions)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderActions)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderHistory)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderHistory)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderRevision)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderRevision)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderAbandon)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderAbandon)
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
    .WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderCommit)
    .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderCommit)
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

app.MapCustomerIndividualEndpoints();
app.MapCustomerDuplicateImportEndpoints();
app.MapCatalogEndpoints();
app.MapPricingEndpoints();
app.MapCatalogOrderEndpoints();
app.MapQuotationEndpoints();
app.MapOrderProgramReferenceRoutes();
app.MapCustomerRepresentativeEndpoints();
app.MapTenantProfilePolicyEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
})
    .WithCoreApiAccess(EndpointAccess.PublicLiveness);

app.MapGet("/health/ready", async (ReadinessStatusCache cache, HttpContext context) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return await cache.IsReadyAsync(context.RequestAborted)
            ? Results.StatusCode(StatusCodes.Status200OK)
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    })
    .WithCoreApiAccess(EndpointAccess.PublicReadiness)
    .ExcludeFromDescription();

app.ValidateCoreApiEndpointAccess();
await app.RunAsync();

public partial class Program;
