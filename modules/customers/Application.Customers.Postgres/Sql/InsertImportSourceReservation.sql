INSERT INTO customers.object_storage_reservations
 (tenant_id,provider_scope,reservation_id,account_id,import_id,idempotency_key,fingerprint,object_key,byte_length,state,
  created_at,updated_at,upload_generation,upload_lease_id,upload_lease_expires_at)
VALUES (@tenant_id,@provider_scope,@reservation_id,@account_id,@import_id,@key,@fingerprint,@object_key,@byte_length,1,
 @now,@now,1,@lease_id,@lease_expires_at);
