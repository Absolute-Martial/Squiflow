UPDATE tenancy.custom_roles
SET availability = 5,
    revision = revision + 1,
    updated_at = @occurred_at,
    retired_at = @occurred_at
WHERE tenant_id = @tenant_id AND role_id = @role_id
  AND availability = 2 AND revision = @expected_role_revision;

UPDATE tenancy.custom_role_assignments
SET availability = 2,
    revision = revision + 1,
    removed_at = @occurred_at
WHERE tenant_id = @tenant_id AND role_id = @role_id AND availability = 1;
