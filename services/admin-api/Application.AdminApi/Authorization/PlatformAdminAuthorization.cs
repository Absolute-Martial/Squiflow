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
    Task<bool> CanOnboardAccountAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanLinkIdentityAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanManageMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanManageTenantLifecycleAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanReadTenantsAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanReadAccountsAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanReadMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanPublishTenantProfileAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
    Task<bool> CanActivateTenantProfileAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
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
    private const string OnboardAccountRelation = "can_onboard_account";
    private const string LinkIdentityRelation = "can_link_identity";
    private const string ManageMembershipsRelation = "can_manage_memberships";
    private const string ManageTenantLifecycleRelation = "can_manage_tenant_lifecycle";
    private const string ReadTenantsRelation = "can_read_tenants";
    private const string ReadAccountsRelation = "can_read_accounts";
    private const string ReadMembershipsRelation = "can_read_memberships";
    private const string PublishTenantProfileRelation = "can_publish_tenant_profile";
    private const string ActivateTenantProfileRelation = "can_activate_tenant_profile";
    private static readonly string[] RequiredPlatformRelations =
    [
        "administrator",
        AccessRelation,
        ProvisionTenantRelation,
        OnboardAccountRelation,
        LinkIdentityRelation,
        ManageMembershipsRelation,
        ManageTenantLifecycleRelation,
        ReadTenantsRelation,
        ReadAccountsRelation,
        ReadMembershipsRelation,
        PublishTenantProfileRelation,
        ActivateTenantProfileRelation,
    ];
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
    private static readonly Action<ILogger, string, Exception?> AuthorizationModelContractMismatch =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, nameof(AuthorizationModelContractMismatch)),
            "Pinned platform OpenFGA model {ModelId} does not satisfy the required platform authorization contract.");

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

    public Task<bool> CanOnboardAccountAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", OnboardAccountRelation, cancellationToken);
    }

    public Task<bool> CanLinkIdentityAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", LinkIdentityRelation, cancellationToken);
    }

    public Task<bool> CanManageMembershipsAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ManageMembershipsRelation, cancellationToken);
    }

    public Task<bool> CanManageTenantLifecycleAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ManageTenantLifecycleRelation, cancellationToken);
    }

    public Task<bool> CanReadTenantsAsync(Guid platformPrincipalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ReadTenantsRelation, cancellationToken);
    }

    public Task<bool> CanReadAccountsAsync(Guid platformPrincipalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ReadAccountsRelation, cancellationToken);
    }

    public Task<bool> CanReadMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ReadMembershipsRelation, cancellationToken);
    }

    public Task<bool> CanPublishTenantProfileAsync(Guid platformPrincipalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", PublishTenantProfileRelation, cancellationToken);
    }

    public Task<bool> CanActivateTenantProfileAsync(Guid platformPrincipalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        return CheckAsync($"user:{platformPrincipalId:N}", ActivateTenantProfileRelation, cancellationToken);
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.RequestTimeout);
        try
        {
            var response = await client.ReadAuthorizationModel(
                new ClientReadAuthorizationModelOptions
                {
                    AuthorizationModelId = configuration.AuthorizationModelId,
                },
                timeout.Token).ConfigureAwait(false);
            if (!HasRequiredPlatformContract(response.AuthorizationModel))
            {
                AuthorizationModelContractMismatch(logger, configuration.AuthorizationModelId, null);
                return false;
            }

            _ = await CheckAsync(
                "user:readiness-probe",
                AccessRelation,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            AuthorizationTimedOut(logger, configuration.AuthorizationModelId, exception);
            return false;
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            AuthorizationUnavailable(logger, configuration.AuthorizationModelId, exception);
            return false;
        }
        catch (AdminAuthorizationProviderUnavailableException)
        {
            return false;
        }
    }

    private bool HasRequiredPlatformContract(AuthorizationModel? model)
    {
        if (model is null || !string.Equals(model.Id, configuration.AuthorizationModelId, StringComparison.Ordinal))
        {
            return false;
        }

        var platform = model.TypeDefinitions?.SingleOrDefault(
            definition => definition is not null &&
                string.Equals(definition.Type, "platform", StringComparison.Ordinal));
        if (platform?.Relations is null)
        {
            return false;
        }

        return RequiredPlatformRelations.All(platform.Relations.ContainsKey);
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
