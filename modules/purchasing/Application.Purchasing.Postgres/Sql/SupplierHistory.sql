SELECT schema_version,event_id,revision,actor_id,authorization_revision,kind,facts::text,snapshot::text,recorded_at
FROM purchasing.supplier_events WHERE tenant_id=@tenant AND supplier_id=@id
AND (@before IS NULL OR revision<@before)
ORDER BY revision DESC LIMIT @limit;
