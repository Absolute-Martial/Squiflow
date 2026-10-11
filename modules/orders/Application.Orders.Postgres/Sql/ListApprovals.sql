SELECT request.request_id,request.order_id,request.order_revision,request.revision,request.state,
    request.requester_id,request.assigned_id,request.created_at,
    draft.state<>'draft' OR draft.revision<>request.order_revision AS subject_stale
FROM orders.approval_requests request JOIN orders.order_drafts draft ON draft.tenant_id=request.tenant_id AND draft.id=request.order_id
WHERE request.tenant_id=@tenant_id AND request.state='pending'
    AND (@recovery OR request.requester_id=@actor_id OR request.assigned_id=@actor_id)
    AND (@after_created_at IS NULL OR (request.created_at,request.request_id)<(@after_created_at,@after_id))
ORDER BY request.created_at DESC,request.request_id DESC LIMIT @limit;
