INSERT INTO tenancy.custom_roles(
    tenant_id, role_id, name, availability, revision, permission_ids,
    created_at, updated_at, retired_at)
VALUES (@tenant_id, @role_id, @name, 2, 1, @permissions, @occurred_at, @occurred_at, NULL);
