INSERT INTO customers.object_storage_reservations
    (tenant_id, provider_scope, reservation_id, idempotency_key, fingerprint, object_key, byte_length,
     state, created_at, updated_at)
VALUES (@tenant_id, @provider_scope, @reservation_id, @key, @fingerprint, @object_key, @byte_length,
        1, @now, @now)
ON CONFLICT DO NOTHING;
