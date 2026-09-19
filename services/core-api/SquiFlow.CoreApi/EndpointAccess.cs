namespace SquiFlow.CoreApi;

public enum EndpointAccess
{
    PublicApplicationBootstrap,
    PublicApiDescription,
    PublicLiveness,
    AuthenticatedAccount,
    AuthenticatedTenantMemberships,
}

public sealed record EndpointAccessMetadata(EndpointAccess Access);
