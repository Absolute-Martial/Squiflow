SELECT reservation_id, object_key, byte_length, fingerprint, state, import_id,
       upload_generation, upload_lease_id, upload_lease_expires_at
FROM customers.object_storage_reservations
WHERE tenant_id=@tenant_id AND provider_scope=@provider_scope AND account_id=@account_id AND idempotency_key=@key
FOR UPDATE;
