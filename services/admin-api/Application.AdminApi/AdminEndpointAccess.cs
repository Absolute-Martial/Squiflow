namespace Application.AdminApi;

internal enum AdminEndpointAccess
{
    PublicHealth,
    ProtectedPlatformAdministration,
}

internal sealed record AdminEndpointAccessMetadata(AdminEndpointAccess Access)
{
    internal bool IsProtected => Access is AdminEndpointAccess.ProtectedPlatformAdministration;
}

internal sealed record AdminEndpointPermissionMetadata(PlatformAdminPermission Permission);
