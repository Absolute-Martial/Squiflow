using Application.AdminApi;
using Application.AdminApi.Authentication;
using Application.AdminApi.Authorization;
using Application.AdminApi.Composition;
using Application.AdminApi.IdentityProvisioning;
using Application.IdentityAccess.Postgres;
using Application.PlatformAdministration.Postgres;
using Application.Tenancy.Postgres;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var apiConfiguration = AdminApiConfiguration.From(builder.Configuration);
var authenticationConfiguration = AdminOidcAuthenticationConfiguration.From(builder.Configuration);
var authorizationConfiguration = AdminOpenFgaAuthorizationConfiguration.From(builder.Configuration);
var identityProvisioningConfiguration = ZitadelIdentityProvisioningConfiguration.From(
    builder.Configuration,
    authenticationConfiguration);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
    options.ConfigureHttpsDefaults(https =>
    {
        // TLS proves possession of the presented device key. Application authority is the
        // exact active certificate fingerprint in PlatformAdministration plus OpenFGA.
        https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        https.ClientCertificateValidation = static (_, _, _) => true;
    });
});

builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
    NpgsqlDataSource.Create(apiConfiguration.DatabaseConnectionString));
builder.Services.AddIdentityAccessPostgres();
builder.Services.AddPlatformAdministrationPostgres();
builder.Services.AddTenancyPostgres();
Application.Profiles.Postgres.ProfilesPostgresRegistration.AddProfilesPostgres(builder.Services);
builder.Services.AddScoped<Application.Orders.Postgres.IOrderProfilePolicySource, Application.AdminApi.OrderProfilePolicySource>();
builder.Services.AddScoped<Application.Orders.Postgres.PostgresOrderDraftStore>();
builder.Services.AddAdminApiAuthentication(authenticationConfiguration);
builder.Services.AddAdminApiAuthorization(authorizationConfiguration);
builder.Services.AddAdminIdentityProvisioning(
    identityProvisioningConfiguration,
    authenticationConfiguration);
builder.Services.AddAdminApiAdmission(apiConfiguration.MaximumConcurrentRequests);
builder.Services.AddAdminApiRequestBudgets(apiConfiguration.ProtectedRequestTimeoutSeconds);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAdminClientCertificateProvider, ConnectionAdminClientCertificateProvider>();
builder.Services.AddScoped<PlatformAdminRequestAuthorizer>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AdminApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseRouting();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseAdminApiRequestBudgets();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAdminApiPlatformAuthorization();

app.MapGet("/health/live", () => Results.Ok())
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.PublicHealth))
    .AllowAnonymous();

app.MapGet(
        "/health/ready",
        async (
            PlatformAdministrationDbContext database,
            IPlatformAdminAuthorization authorization,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var databaseReady = await database.Database
                    .CanConnectAsync(cancellationToken)
                    .ConfigureAwait(false);
                var authorizationReady = databaseReady &&
                    await authorization.IsReadyAsync(cancellationToken).ConfigureAwait(false);
                return databaseReady && authorizationReady
                    ? Results.StatusCode(StatusCodes.Status200OK)
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception exception) when (
                exception is NpgsqlException or
                AdminAuthorizationProviderUnavailableException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        })
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.PublicHealth))
    .AllowAnonymous();

app.MapGet("/api/v1/platform/access", PlatformAdminAccessEndpoint.GetAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.Access))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.PlatformAccess))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost("/api/v1/platform/tenants", TenantProvisioningEndpoint.PostAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ProvisionTenant))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.TenantProvision))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost("/api/v1/platform/accounts", AccountOnboardingEndpoint.PostAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.OnboardAccount))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.AccountOnboard))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost(
        "/api/v1/platform/accounts/{accountId:guid}/identities",
        AccountOnboardingEndpoint.LinkAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.LinkIdentity))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.IdentityLink))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost(
        "/api/v1/platform/tenants/{tenantId:guid}/memberships",
        MembershipLifecycleEndpoint.InviteAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ManageMemberships))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.MembershipInvite))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost(
        "/api/v1/platform/tenants/{tenantId:guid}/memberships/initial-owner",
        MembershipLifecycleEndpoint.BootstrapOwnerAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ManageMemberships))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.MembershipBootstrapOwner))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost(
        "/api/v1/platform/tenants/{tenantId:guid}/memberships/{accountId:guid}/{operation}",
        MembershipLifecycleEndpoint.TransitionAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ManageMemberships))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.MembershipTransition))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost(
        "/api/v1/platform/tenants/{tenantId:guid}/lifecycle/{operation}",
        TenantLifecycleEndpoint.PostAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ManageTenantLifecycle))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.TenantLifecycleTransition))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);


app.MapGet("/api/v1/platform/tenants", PlatformRegistryEndpoint.BrowseTenantsAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadTenants))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryTenantsBrowse))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapGet("/api/v1/platform/tenants/{tenantId:guid}", PlatformRegistryEndpoint.GetTenantAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadTenants))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryTenantDetail))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapGet("/api/v1/platform/accounts", PlatformRegistryEndpoint.BrowseAccountsAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadAccounts))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryAccountsBrowse))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapGet("/api/v1/platform/accounts/{accountId:guid}", PlatformRegistryEndpoint.GetAccountAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadAccounts))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryAccountDetail))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapGet("/api/v1/platform/tenants/{tenantId:guid}/memberships", PlatformRegistryEndpoint.BrowseMembershipsAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadMemberships))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryMembershipsBrowse))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapGet("/api/v1/platform/tenants/{tenantId:guid}/memberships/{accountId:guid}", PlatformRegistryEndpoint.GetMembershipAsync)
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadMemberships))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryMembershipDetail))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapTenantProfileEndpoints();
app.MapPost("/api/v1/platform/tenants/{tenantId:guid}/profiles/legacy-orders/{orderId:guid}/assign",
        LegacyOrderProfileAssignmentEndpoint.PostAsync)
    .WithName("AssignLegacyOrderProfile")
    .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
    .WithMetadata(new AdminEndpointPermissionMetadata(PlatformAdminPermission.ActivateTenantProfile))
    .WithMetadata(new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.LegacyOrderProfileAssignment))
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.ValidateAdminApiEndpointAccess();

await app.RunAsync();

public partial class Program;
