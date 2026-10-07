UPDATE customers.object_storage_reservations
SET state = 2, updated_at = @now
WHERE tenant_id = @tenant_id AND provider_scope = @provider_scope AND reservation_id = @reservation_id
  AND state IN (1, 4);
