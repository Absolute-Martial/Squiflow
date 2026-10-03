using Application.AdminApi;
using Application.AdminApi.Authentication;
using Application.AdminApi.Authorization;
using Application.AdminApi.Composition;
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
builder.Services.AddPlatformAdministrationPostgres();
builder.Services.AddTenancyPostgres();
builder.Services.AddAdminApiAuthentication(authenticationConfiguration);
builder.Services.AddAdminApiAuthorization(authorizationConfiguration);
builder.Services.AddAdminApiAdmission(apiConfiguration.MaximumConcurrentRequests);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAdminClientCertificateProvider, ConnectionAdminClientCertificateProvider>();
builder.Services.AddScoped<PlatformAdminRequestAuthorizer>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AdminApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok())
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
    .AllowAnonymous();

app.MapGet("/api/v1/platform/access", PlatformAdminAccessEndpoint.GetAsync)
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

app.MapPost("/api/v1/platform/tenants", TenantProvisioningEndpoint.PostAsync)
    .RequireAuthorization()
    .RequireRateLimiting(AdminApiAdmission.PolicyName);

await app.RunAsync();

public partial class Program;
