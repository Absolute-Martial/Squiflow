SELECT reservation_id, object_key, byte_length, fingerprint, state
FROM customers.object_storage_reservations
WHERE tenant_id = @tenant_id AND provider_scope = @provider_scope AND idempotency_key = @key
FOR UPDATE;
