UPDATE customers.object_storage_reservations
SET state=1, upload_generation=upload_generation+1, upload_lease_id=@lease_id, upload_lease_expires_at=@lease_expires_at, updated_at=@now
WHERE tenant_id=@tenant_id AND reservation_id=@reservation_id AND account_id=@account_id AND state IN (1,3,4)
 AND (upload_lease_expires_at IS NULL OR upload_lease_expires_at<=@now)
RETURNING upload_generation;
