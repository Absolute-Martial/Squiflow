UPDATE orders.approval_requests SET assigned_id=@assigned_id,revision=@revision,state=@state,updated_at=@updated_at
WHERE tenant_id=@tenant_id AND request_id=@request_id AND revision=@expected_revision AND state='pending';
