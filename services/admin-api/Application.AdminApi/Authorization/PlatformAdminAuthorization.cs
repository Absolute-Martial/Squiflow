using Application.PlatformAdministration;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Exceptions;
using OpenFga.Sdk.Model;

namespace Application.AdminApi.Authorization;

internal interface IPlatformAdminAuthorization
{
    Task<bool> CanAccessAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanProvisionTenantAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

internal sealed class OpenFgaPlatformAdminAuthorization(
    IOpenFgaClient client,
    AdminOpenFgaAuthorizationConfiguration configuration,
    ILogger<OpenFgaPlatformAdminAuthorization> logger) : IPlatformAdminAuthorization
{
    private const string PlatformObject = "platform:root";
    private const string AccessRelation = "can_access_admin";
    private const string ProvisionTenantRelation = "can_provision_tenant";
    private static readonly Action<ILogger, string, Exception?> AuthorizationTimedOut =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(AuthorizationTimedOut)),
            "Platform OpenFGA authorization timed out using model {ModelId}.");
    private static readonly Action<ILogger, string, Exception?> AuthorizationUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(AuthorizationUnavailable)),
            "Platform OpenFGA authorization was unavailable using model {ModelId}.");

    public Task<bool> CanAccessAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", AccessRelation, cancellationToken);
    }

    public Task<bool> CanProvisionTenantAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ProvisionTenantRelation, cancellationToken);
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await CheckAsync(
                "user:readiness-probe",
                AccessRelation,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AdminAuthorizationProviderUnavailableException)
        {
            return false;
        }
    }

    private async Task<bool> CheckAsync(
        string user,
        string relation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.RequestTimeout);
        try
        {
            var response = await client.Check(
                new ClientCheckRequest
                {
                    User = user,
                    Relation = relation,
                    Object = PlatformObject,
                },
                new ClientCheckOptions
                {
                    StoreId = configuration.StoreId,
                    AuthorizationModelId = configuration.AuthorizationModelId,
                    Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
                },
                timeout.Token).ConfigureAwait(false);
            return response.Allowed is true;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            AuthorizationTimedOut(logger, configuration.AuthorizationModelId, exception);
            throw new AdminAuthorizationProviderUnavailableException(
                "The platform authorization provider timed out.",
                exception);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            AuthorizationUnavailable(logger, configuration.AuthorizationModelId, exception);
            throw new AdminAuthorizationProviderUnavailableException(
                "The platform authorization provider was unavailable.",
                exception);
        }
    }
}

internal sealed class AdminAuthorizationProviderUnavailableException(
    string message,
    Exception innerException) : Exception(message, innerException);

internal static class AdminApiAuthorizationRegistration
{
    internal static IServiceCollection AddAdminApiAuthorization(
        this IServiceCollection services,
        AdminOpenFgaAuthorizationConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<IOpenFgaClient>(_ =>
            new OpenFgaClient(configuration.ToClientConfiguration()));
        services.AddSingleton<IPlatformAdminAuthorization, OpenFgaPlatformAdminAuthorization>();
        return services;
    }
}
