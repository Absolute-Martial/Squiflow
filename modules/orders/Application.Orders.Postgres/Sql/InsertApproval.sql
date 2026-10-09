INSERT INTO orders.approval_requests(tenant_id,request_id,order_id,requester_id,assigned_id,revision_author_id,
    order_revision,subject,subject_fingerprint,revision,state,created_at,updated_at)
VALUES(@tenant_id,@request_id,@order_id,@requester_id,@assigned_id,@revision_author_id,@order_revision,
    @subject::jsonb,@subject_fingerprint,@revision,@state,@created_at,@updated_at);
