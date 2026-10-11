SELECT request_id,request_revision,response_json::text,operation,actor_id,authorization_revision,recorded_at
FROM orders.approval_events WHERE tenant_id=@tenant_id AND request_id=@request_id
    AND (@before_revision IS NULL OR request_revision<@before_revision)
ORDER BY request_revision DESC LIMIT @limit;
