INSERT INTO tenancy.tenant_authorization_state(tenant_id, revision, updated_at)
VALUES (@tenant_id, 1, @occurred_at)
ON CONFLICT (tenant_id) DO NOTHING;
