UPDATE orders.order_drafts SET revision=revision+1
WHERE tenant_id=@tenant_id AND id=@order_id AND state='draft' AND revision=@expected_revision;
