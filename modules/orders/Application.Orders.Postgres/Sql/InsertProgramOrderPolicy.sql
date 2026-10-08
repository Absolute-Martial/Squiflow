INSERT INTO orders.program_order_metadata(tenant_id,order_id,profile_id,policy_revision_id,require_reference,external_reference,bound_at)
VALUES(@tenant_id,@order_id,@profile_id,@policy_revision_id,@require_reference,@external_reference,@bound_at);
