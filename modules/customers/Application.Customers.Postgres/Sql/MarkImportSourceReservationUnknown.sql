UPDATE customers.object_storage_reservations
SET state = 4, updated_at = @now
WHERE tenant_id = @tenant_id AND reservation_id = @reservation_id AND state = 1;
