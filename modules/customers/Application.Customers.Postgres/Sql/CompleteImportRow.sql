-- Commit-point fencing. The pre-row claim lock is an ownership pre-check, not the authority:
-- this predicate re-asserts that the claiming work row still belongs to this exact worker,
-- generation and live lease while the row result is written, so a superseded owner can never
-- complete a row the current owner is processing. Zero affected rows means the claim is lost.
UPDATE customers.import_rows SET status=@status,customer_id=@customer_id,error_code=@error_code,
    error_message=@error_message,attempts=attempts+1,processed_at=clock_timestamp()
WHERE tenant_id=@tenant_id AND import_id=@import_id AND row_number=@row_number AND status IN (1,5)
  AND EXISTS (SELECT 1 FROM customers.import_work AS claim
      WHERE claim.tenant_id=customers.import_rows.tenant_id AND claim.import_id=customers.import_rows.import_id
        AND claim.work_id=@work_id AND claim.worker_id=@worker_id AND claim.generation=@generation
        AND claim.status=2 AND claim.lease_expires_at>clock_timestamp());
