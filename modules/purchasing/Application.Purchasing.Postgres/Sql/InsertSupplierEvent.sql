INSERT INTO purchasing.supplier_events(tenant_id,supplier_id,event_id,revision,actor_id,authorization_revision,kind,facts,snapshot,recorded_at)
VALUES(@tenant,@id,@event,@revision,@actor,@authorization,@kind,@facts::jsonb,@snapshot::jsonb,@at);
