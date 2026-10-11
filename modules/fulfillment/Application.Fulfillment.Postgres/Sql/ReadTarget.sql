SELECT CASE WHEN octet_length(e.facts::text)<=65536 THEN e.facts::text ELSE '{}' END,e.event_id,e.revision,e.actor_id,e.authorization_revision,e.recorded_at,a.active,
 (EXISTS(SELECT 1 FROM fulfillment.entry_activity d WHERE d.tenant_id=e.tenant_id AND d.order_id=e.order_id AND d.active AND d.target_event_id=e.event_id AND d.kind=5)
 OR (a.kind IN(1,5) AND EXISTS(SELECT 1 FROM fulfillment.entry_activity d WHERE d.tenant_id=e.tenant_id AND d.order_id=e.order_id AND d.active AND d.revision>e.revision))),
 e.schema_version,a.schema_version,e.kind
 FROM fulfillment.events e JOIN fulfillment.entry_activity a USING(tenant_id,order_id,event_id)
 WHERE e.tenant_id=@tenant AND e.order_id=@order AND e.event_id=@target;
