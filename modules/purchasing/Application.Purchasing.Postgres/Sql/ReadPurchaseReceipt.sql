SELECT e.schema_version,e.event_id,e.revision,e.actor_id,e.authorization_revision,e.kind,e.facts::text,e.snapshot::text,e.recorded_at,
 r.schema_version,r.fingerprint,r.purchase_id,r.revision,r.response::text,a.schema_version,a.created_at,a.supplier_id,a.source::text,e.target_event_id
FROM purchasing.purchase_command_receipts r
JOIN purchasing.purchase_events e ON (e.tenant_id,e.purchase_id,e.revision)=(r.tenant_id,r.purchase_id,r.revision)
JOIN purchasing.purchases a ON (a.tenant_id,a.purchase_id)=(r.tenant_id,r.purchase_id)
WHERE r.tenant_id=@tenant AND r.actor_id=@actor AND r.operation=@operation AND r.idempotency_key=@key;
