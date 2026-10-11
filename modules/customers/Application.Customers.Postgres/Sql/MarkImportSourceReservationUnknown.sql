UPDATE customers.object_storage_reservations SET state=4,updated_at=@now,upload_lease_id=NULL,upload_lease_expires_at=NULL
WHERE tenant_id=@tenant_id AND reservation_id=@reservation_id AND state IN(1,4);
