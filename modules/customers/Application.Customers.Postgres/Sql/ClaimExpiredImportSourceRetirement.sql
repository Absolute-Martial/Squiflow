WITH candidate AS (
 SELECT object_key,retirement_generation FROM customers.import_source_objects s
 WHERE s.tenant_id=@tenant_id AND s.state<>6
 AND ((s.staging_expires_at IS NOT NULL AND s.staging_expires_at<=@now)
   OR (s.retention=1 AND s.expires_at IS NOT NULL AND s.expires_at<=@now))
 AND (s.state<>7 OR s.retirement_lease_expires_at<=@now)
 AND NOT EXISTS (SELECT 1 FROM customers.object_storage_reservations r
   WHERE r.tenant_id=s.tenant_id AND r.object_key=s.object_key AND r.state IN(1,4) AND r.upload_lease_expires_at>@now)
 ORDER BY COALESCE(s.retirement_lease_expires_at,s.staging_expires_at,s.expires_at),s.object_key
 FOR UPDATE SKIP LOCKED LIMIT 1
)
UPDATE customers.import_source_objects s SET state=7,retirement_generation=c.retirement_generation+1,
 retirement_lease_id=@lease_id,retirement_lease_expires_at=@lease_expires_at
FROM candidate c WHERE s.tenant_id=@tenant_id AND s.object_key=c.object_key
RETURNING s.object_key,s.provider_scope,s.byte_length,s.sha256,s.retirement_generation,s.retirement_lease_id,s.retirement_lease_expires_at;
