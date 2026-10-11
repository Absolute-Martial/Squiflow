WITH reservations AS MATERIALIZED (
 SELECT reservation_id,state,byte_length FROM customers.object_storage_reservations
 WHERE tenant_id=@tenant_id AND object_key=@object_key FOR UPDATE
), totals AS (
 SELECT COALESCE(sum(byte_length) FILTER(WHERE state IN(1,4)),0)::bigint AS reserved,
        COALESCE(sum(byte_length) FILTER(WHERE state=2),0)::bigint AS retained FROM reservations
), completed AS (
 UPDATE customers.import_source_objects s SET state=@state,failure_code=@failure_code,
  retirement_fenced=@deleted,retirement_lease_id=NULL,retirement_lease_expires_at=NULL
 FROM totals t,customers.object_storage_usage u
 WHERE s.tenant_id=@tenant_id AND s.object_key=@object_key AND s.state=7
  AND s.retirement_generation=@generation AND s.retirement_lease_id=@lease_id AND s.retirement_lease_expires_at>@now
  AND u.tenant_id=s.tenant_id AND u.provider_scope=s.provider_scope
  AND (NOT @deleted OR (u.reserved_bytes>=t.reserved AND u.retained_bytes>=t.retained))
 RETURNING s.provider_scope,t.reserved,t.retained
), released AS (
 UPDATE customers.object_storage_reservations r SET state=3,updated_at=@now,upload_lease_id=NULL,upload_lease_expires_at=NULL
 WHERE @deleted AND r.tenant_id=@tenant_id AND r.object_key=@object_key AND r.state IN(1,2,4)
  AND EXISTS(SELECT 1 FROM completed) RETURNING r.reservation_id
), usage AS (
 UPDATE customers.object_storage_usage u SET reserved_bytes=u.reserved_bytes-c.reserved,
  retained_bytes=u.retained_bytes-c.retained,updated_at=@now
 FROM completed c WHERE @deleted AND u.tenant_id=@tenant_id AND u.provider_scope=c.provider_scope
 RETURNING u.provider_scope
)
SELECT EXISTS(SELECT 1 FROM completed);
