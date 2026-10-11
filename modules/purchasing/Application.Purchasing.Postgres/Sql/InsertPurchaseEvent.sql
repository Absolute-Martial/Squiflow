INSERT INTO purchasing.purchase_events(tenant_id,purchase_id,event_id,revision,actor_id,authorization_revision,kind,facts,snapshot,recorded_at,target_event_id)
VALUES(@tenant,@id,@event,@revision,@actor,@authorization,@kind,@facts::jsonb,@snapshot::jsonb,@at,@target);
