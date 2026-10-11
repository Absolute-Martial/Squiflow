SELECT request.*,event.response_json::text,event.actor_id,event.authorization_revision,event.operation,event.recorded_at
FROM orders.approval_requests request JOIN orders.approval_events event
    ON event.tenant_id=request.tenant_id AND event.request_id=request.request_id AND event.request_revision=request.revision
WHERE request.tenant_id=@tenant_id AND request.order_id=@order_id AND request.state='pending';
