SELECT e.schema_version,e.event_id,e.revision,e.actor_id,e.authorization_revision,e.kind,e.facts::text,e.snapshot::text,e.recorded_at,
 r.schema_version,r.fingerprint,r.supplier_id,r.revision,r.response::text,a.schema_version,a.created_at
FROM purchasing.supplier_command_receipts r
JOIN purchasing.supplier_events e ON (e.tenant_id,e.supplier_id,e.revision)=(r.tenant_id,r.supplier_id,r.revision)
JOIN purchasing.suppliers a ON (a.tenant_id,a.supplier_id)=(r.tenant_id,r.supplier_id)
WHERE r.tenant_id=@tenant AND r.actor_id=@actor AND r.operation=@operation AND r.idempotency_key=@key;
