SELECT provider_scope, byte_length, state
FROM customers.object_storage_reservations
WHERE tenant_id = @tenant_id AND reservation_id = @reservation_id
FOR UPDATE;
