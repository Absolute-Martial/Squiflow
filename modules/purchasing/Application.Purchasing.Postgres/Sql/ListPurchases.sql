SELECT a.snapshot::text, a.schema_version, a.revision, a.purchase_id, a.created_at, a.updated_at, a.supplier_id, a.source::text,
CASE WHEN e.schema_version=1 AND r.schema_version=1 AND e.snapshot=a.snapshot
     AND r.response->>'schemaVersion'='1' AND r.response->'snapshot'=a.snapshot
     AND r.operation=e.kind AND r.actor_id=e.actor_id AND e.recorded_at=a.updated_at
     AND e.facts->>'schemaVersion'='1' AND e.facts->>'revision'=a.revision::text
     AND e.facts->>'eventId'=e.event_id::text AND e.facts->>'actorAccountId'=e.actor_id::text
     AND e.facts->>'authorizationRevision'=e.authorization_revision::text
     AND e.facts->>'kind'=e.kind::text
     THEN true ELSE false END, e.facts::text, r.response::text, e.target_event_id
FROM purchasing.purchases a
LEFT JOIN purchasing.purchase_events e ON (e.tenant_id,e.purchase_id,e.revision)=(a.tenant_id,a.purchase_id,a.revision)
LEFT JOIN purchasing.purchase_command_receipts r ON (r.tenant_id,r.purchase_id,r.revision)=(a.tenant_id,a.purchase_id,a.revision)
WHERE a.tenant_id=@tenant AND (@after_at IS NULL OR (a.created_at,a.purchase_id) < (@after_at,@after_id))
ORDER BY a.created_at DESC,a.purchase_id DESC LIMIT @limit;
