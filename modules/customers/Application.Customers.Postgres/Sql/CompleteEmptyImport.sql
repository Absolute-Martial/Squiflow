UPDATE customers.import_work w SET status=3,completed_at=COALESCE(completed_at,clock_timestamp()),worker_id=NULL,lease_expires_at=NULL
WHERE tenant_id=@tenant_id AND import_id=@import_id
  AND NOT EXISTS(SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id AND r.import_id=w.import_id AND r.status IN(1,5));
