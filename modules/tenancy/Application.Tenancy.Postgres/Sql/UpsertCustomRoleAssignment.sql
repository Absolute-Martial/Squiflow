INSERT INTO tenancy.custom_role_assignments(
    tenant_id, role_id, account_id, availability, revision, assigned_at, removed_at)
VALUES (
    @tenant_id, @role_id, @account_id, @availability, 1, @occurred_at,
    CASE WHEN @availability = 1 THEN NULL ELSE @occurred_at END)
ON CONFLICT (tenant_id, role_id, account_id) DO UPDATE
SET availability = EXCLUDED.availability,
    revision = tenancy.custom_role_assignments.revision + 1,
    assigned_at = CASE WHEN EXCLUDED.availability = 1 THEN EXCLUDED.assigned_at ELSE tenancy.custom_role_assignments.assigned_at END,
    removed_at = CASE WHEN EXCLUDED.availability = 1 THEN NULL ELSE EXCLUDED.removed_at END;
