SELECT provider_scope, object_key, byte_length, state, account_id, import_id,
       upload_generation, upload_lease_id, upload_lease_expires_at, idempotency_key, fingerprint
FROM customers.object_storage_reservations WHERE tenant_id=@tenant_id AND reservation_id=@reservation_id;
