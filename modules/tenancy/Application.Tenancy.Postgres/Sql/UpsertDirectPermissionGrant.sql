INSERT INTO tenancy.tenant_permission_grants(
    tenant_id, account_id, permission_id, is_active, revision, granted_at, revoked_at)
VALUES (
    @tenant_id, @account_id, @permission_id, @is_active, 1, @occurred_at,
    CASE WHEN @is_active THEN NULL ELSE @occurred_at END)
ON CONFLICT (tenant_id, account_id, permission_id) DO UPDATE
SET is_active = EXCLUDED.is_active,
    revision = tenancy.tenant_permission_grants.revision + 1,
    granted_at = CASE WHEN EXCLUDED.is_active THEN EXCLUDED.granted_at ELSE tenancy.tenant_permission_grants.granted_at END,
    revoked_at = CASE WHEN EXCLUDED.is_active THEN NULL ELSE EXCLUDED.revoked_at END;
