UPDATE tenancy.memberships
SET availability = @availability,
    revision = @revision,
    activated_at = @activated_at,
    suspended_at = @suspended_at,
    removed_at = @removed_at
WHERE tenant_id = @tenant_id AND account_id = @account_id;
