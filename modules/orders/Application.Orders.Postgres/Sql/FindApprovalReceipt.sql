SELECT request_id,request_revision,response_json::text,fingerprint,operation,actor_id,authorization_revision,recorded_at
FROM orders.approval_events
WHERE tenant_id=@tenant_id AND actor_id=@actor_id
    AND command_scope=CASE WHEN @operation IN ('approve','reject') THEN 'decide' ELSE @operation END AND idempotency_key=@key;
