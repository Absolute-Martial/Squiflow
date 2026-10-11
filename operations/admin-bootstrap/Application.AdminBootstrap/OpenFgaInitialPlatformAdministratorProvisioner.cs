using Application.PlatformAdministration;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Exceptions;
using OpenFga.Sdk.Model;

namespace Application.AdminBootstrap;

internal sealed class OpenFgaInitialPlatformAdministratorProvisioner(
    IOpenFgaClient client,
    PlatformOpenFgaConfiguration configuration) : IInitialPlatformAdministratorProvisioner
{
    private const string PlatformObject = "platform:root";
    private const string AdministratorRelation = "administrator";
    private const string AccessRelation = "can_access_admin";

    public async Task EnsureAdministratorAsync(
        Guid platformPrincipalId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(platformPrincipalId, Guid.Empty);
        var user = $"user:{platformPrincipalId:N}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.RequestTimeout);

        try
        {
            await client.Write(
                new ClientWriteRequest
                {
                    Writes =
                    [
                        new ClientTupleKey
                        {
                            User = user,
                            Relation = AdministratorRelation,
                            Object = PlatformObject,
                        },
                    ],
                },
                new ClientWriteOptions
                {
                    StoreId = configuration.StoreId,
                    AuthorizationModelId = configuration.AuthorizationModelId,
                    Conflict = new ConflictOptions
                    {
                        OnDuplicateWrites = OnDuplicateWrites.Ignore,
                    },
                },
                timeout.Token).ConfigureAwait(false);

            var check = await client.Check(
                new ClientCheckRequest
                {
                    User = user,
                    Relation = AccessRelation,
                    Object = PlatformObject,
                },
                new ClientCheckOptions
                {
                    StoreId = configuration.StoreId,
                    AuthorizationModelId = configuration.AuthorizationModelId,
                    Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
                },
                timeout.Token).ConfigureAwait(false);
            if (check.Allowed is not true)
            {
                throw new InvalidOperationException(
                    "The pinned platform authorization model did not confirm initial administrator access.");
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PlatformAuthorizationProviderUnavailableException(
                "The platform authorization provider timed out.",
                exception);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            throw new PlatformAuthorizationProviderUnavailableException(
                "The platform authorization provider was unavailable.",
                exception);
        }
    }
}
