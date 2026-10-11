SELECT CASE WHEN octet_length(e.facts::text)<=65536 THEN e.facts::text ELSE '{}' END,e.event_id,e.revision,e.actor_id,e.authorization_revision,e.recorded_at,a.active,e.schema_version,a.schema_version,e.kind
 FROM fulfillment.events e JOIN fulfillment.entry_activity a USING(tenant_id,order_id,event_id)
 WHERE e.tenant_id=@tenant AND e.order_id=@order AND (@before::bigint IS NULL OR e.revision<@before)
 ORDER BY e.revision DESC LIMIT @limit;
