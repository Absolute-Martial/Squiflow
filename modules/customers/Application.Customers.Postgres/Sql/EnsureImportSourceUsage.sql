INSERT INTO customers.object_storage_usage
    (tenant_id, provider_scope, maximum_bytes, reserved_bytes, retained_bytes, updated_at)
VALUES (@tenant_id, @provider_scope, @maximum_bytes, 0, 0, @now)
ON CONFLICT (tenant_id, provider_scope) DO NOTHING;
