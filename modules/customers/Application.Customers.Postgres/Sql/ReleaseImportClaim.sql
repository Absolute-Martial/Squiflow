UPDATE customers.import_work w SET worker_id=NULL,lease_expires_at=NULL,
    last_error=CASE WHEN @denied THEN 'authority_changed' ELSE
      CASE WHEN EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
         AND r.import_id=w.import_id AND r.status=5) THEN 'row_processing_failed' ELSE NULL END END,
    status=CASE WHEN @denied THEN 4
      WHEN EXISTS(SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id AND r.import_id=w.import_id AND r.status IN(1,5)) THEN
          CASE WHEN EXISTS(SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id AND r.import_id=w.import_id AND r.status IN(1,5) AND r.attempts<3) THEN 1 ELSE 4 END
      ELSE 3 END,
    next_attempt_at=CASE WHEN EXISTS(SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id AND r.import_id=w.import_id AND r.status=5) THEN clock_timestamp()+interval '5 seconds' ELSE NULL END,
    completed_at=CASE WHEN NOT EXISTS(SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id AND r.import_id=w.import_id AND r.status IN(1,5)) THEN clock_timestamp() ELSE NULL END
WHERE tenant_id=@tenant_id AND work_id=@work_id AND worker_id=@worker_id AND generation=@generation;
