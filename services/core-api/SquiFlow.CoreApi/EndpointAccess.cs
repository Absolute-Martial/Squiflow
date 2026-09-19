namespace SquiFlow.CoreApi;

public enum EndpointAccess
{
    PublicApplicationBootstrap,
    PublicLiveness,
    AuthenticatedAccount,
    AuthenticatedTenantMemberships,
}

public sealed record EndpointAccessMetadata(EndpointAccess Access);
