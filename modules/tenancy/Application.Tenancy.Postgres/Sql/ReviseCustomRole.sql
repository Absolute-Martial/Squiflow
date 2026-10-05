UPDATE tenancy.custom_roles
SET name = @name,
    permission_ids = @permissions,
    revision = revision + 1,
    updated_at = @occurred_at
WHERE tenant_id = @tenant_id AND role_id = @role_id
  AND availability = 2 AND revision = @expected_role_revision;
