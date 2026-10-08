WITH candidate AS (
    SELECT object_key, retirement_generation
    FROM customers.import_source_objects AS source
    WHERE source.tenant_id = @tenant_id AND source.retention = 1
      AND source.expires_at IS NOT NULL AND source.expires_at <= @now
      AND (
          source.state IN (3, 5)
          OR (source.state = 7 AND source.retirement_lease_expires_at IS NOT NULL
              AND source.retirement_lease_expires_at <= @now)
      )
      AND NOT EXISTS (
           SELECT 1 FROM customers.object_storage_reservations AS reservation
           WHERE reservation.tenant_id = source.tenant_id AND reservation.object_key = source.object_key
             AND reservation.state IN (1, 4))
    ORDER BY COALESCE(source.retirement_lease_expires_at, source.expires_at), source.object_key
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
UPDATE customers.import_source_objects AS source
SET state = 7,
    retirement_generation = candidate.retirement_generation + 1,
    retirement_lease_id = @lease_id,
    retirement_lease_expires_at = @lease_expires_at
FROM candidate
WHERE source.tenant_id = @tenant_id AND source.object_key = candidate.object_key
RETURNING source.object_key, source.provider_scope, source.byte_length, source.sha256,
          source.retirement_generation, source.retirement_lease_id, source.retirement_lease_expires_at;
