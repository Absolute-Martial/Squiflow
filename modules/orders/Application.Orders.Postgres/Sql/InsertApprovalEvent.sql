INSERT INTO orders.approval_events(tenant_id,request_id,request_revision,actor_id,authorization_revision,
    operation,idempotency_key,fingerprint,response_json,recorded_at)
VALUES(@tenant_id,@request_id,@revision,@actor_id,@authorization_revision,@operation,@key,@fingerprint,@response_json::jsonb,@updated_at)
ON CONFLICT (tenant_id,actor_id,command_scope,idempotency_key) DO NOTHING RETURNING 1;
