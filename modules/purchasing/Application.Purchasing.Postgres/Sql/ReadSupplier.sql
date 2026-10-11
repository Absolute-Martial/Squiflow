SELECT a.snapshot::text, a.schema_version, a.revision, a.supplier_id, a.created_at, a.updated_at,
CASE WHEN e.schema_version=1 AND r.schema_version=1 AND e.snapshot=a.snapshot
     AND r.response->>'schemaVersion'='1' AND r.response->'snapshot'=a.snapshot
     AND r.operation=e.kind AND r.actor_id=e.actor_id AND e.recorded_at=a.updated_at
     AND e.facts->>'schemaVersion'='1' AND e.facts->>'revision'=a.revision::text
     AND e.facts->>'eventId'=e.event_id::text AND e.facts->>'actorAccountId'=e.actor_id::text
     AND e.facts->>'authorizationRevision'=e.authorization_revision::text
     AND e.facts->>'kind'=e.kind::text
     THEN true ELSE false END, e.facts::text, r.response::text
FROM purchasing.suppliers a
LEFT JOIN purchasing.supplier_events e ON (e.tenant_id,e.supplier_id,e.revision)=(a.tenant_id,a.supplier_id,a.revision)
LEFT JOIN purchasing.supplier_command_receipts r ON (r.tenant_id,r.supplier_id,r.revision)=(a.tenant_id,a.supplier_id,a.revision)
WHERE a.tenant_id=@tenant AND a.supplier_id=@id;
