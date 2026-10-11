WITH candidate AS (
 SELECT work_id FROM customers.import_work
 WHERE tenant_id = @tenant_id AND status IN (1,2,4)
   AND (last_error IS NULL OR last_error NOT IN ('authority_changed','legacy_work_requires_replan'))
   AND (next_attempt_at IS NULL OR next_attempt_at <= clock_timestamp())
   AND (lease_expires_at IS NULL OR lease_expires_at <= clock_timestamp())
   AND (EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id = @tenant_id
       AND r.import_id = import_work.import_id AND r.status IN (1,5) AND r.attempts < 3)
       OR NOT EXISTS(SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=@tenant_id
         AND r.import_id=import_work.import_id AND r.status IN(1,5)))
 ORDER BY created_at, work_id LIMIT 1 FOR UPDATE SKIP LOCKED
)
UPDATE customers.import_work w SET status = 2, generation = generation+1, worker_id=@worker_id,
    lease_expires_at=clock_timestamp() + @lease, completed_at=NULL
FROM candidate c WHERE w.tenant_id=@tenant_id AND w.work_id=c.work_id
RETURNING w.work_id,w.import_id,w.account_id,w.authorization_revision,w.generation,w.lease_expires_at;
