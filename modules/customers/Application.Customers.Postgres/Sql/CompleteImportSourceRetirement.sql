WITH completed AS (
    UPDATE customers.import_source_objects AS source
    SET state = @state,
        failure_code = @failure_code,
        retirement_lease_id = NULL,
        retirement_lease_expires_at = NULL
    WHERE source.tenant_id = @tenant_id AND source.object_key = @object_key AND source.state = 7
      AND source.retirement_generation = @generation
      AND source.retirement_lease_id = @lease_id
      AND source.retirement_lease_expires_at IS NOT NULL
      AND source.retirement_lease_expires_at > @now
      AND NOT EXISTS (
          SELECT 1 FROM customers.object_storage_reservations AS reservation
          WHERE reservation.tenant_id = source.tenant_id AND reservation.object_key = source.object_key
            AND reservation.state IN (1, 4))
      AND (
          @deleted = false
          OR EXISTS (
              SELECT 1 FROM customers.object_storage_usage AS usage
              WHERE usage.tenant_id = source.tenant_id AND usage.provider_scope = source.provider_scope
                AND usage.retained_bytes >= source.byte_length))
    RETURNING source.provider_scope, source.byte_length
)
UPDATE customers.object_storage_usage AS usage
SET retained_bytes = usage.retained_bytes - completed.byte_length,
    updated_at = @now
FROM completed
WHERE @deleted
  AND usage.tenant_id = @tenant_id
  AND usage.provider_scope = completed.provider_scope;
