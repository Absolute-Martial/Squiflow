SELECT work_id FROM customers.import_work WHERE tenant_id=@tenant_id AND work_id=@work_id
    AND worker_id=@worker_id AND generation=@generation AND lease_expires_at>clock_timestamp() AND status=2
FOR UPDATE;
