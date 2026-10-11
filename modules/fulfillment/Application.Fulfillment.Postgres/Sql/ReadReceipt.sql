SELECT CASE WHEN octet_length(e.facts::text)<=65536 THEN e.facts::text ELSE '{}' END,e.event_id,e.revision,e.actor_id,e.authorization_revision,e.recorded_at,e.schema_version,e.kind,
       r.fingerprint,CASE WHEN octet_length(r.response::text)<=65536 THEN r.response::text ELSE '{}' END,r.revision,r.schema_version,w.schema_version,CASE WHEN octet_length(w.source::text)<=65536 THEN w.source::text ELSE '{}' END,w.work_id,w.created_at,r.order_id
 FROM fulfillment.command_receipts r
 JOIN fulfillment.works w USING(tenant_id,order_id)
 JOIN fulfillment.events e ON e.tenant_id=r.tenant_id AND e.order_id=r.order_id AND e.revision=r.revision
 WHERE r.tenant_id=@tenant AND r.actor_id=@actor AND r.operation=@operation AND r.idempotency_key=@key;
