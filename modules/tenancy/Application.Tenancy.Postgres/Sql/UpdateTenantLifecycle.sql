UPDATE tenancy.tenants
SET availability = @availability,
    revision = @revision,
    suspended_at = @suspended_at
WHERE id = @tenant_id;
