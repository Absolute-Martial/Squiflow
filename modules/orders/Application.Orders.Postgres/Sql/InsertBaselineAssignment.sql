INSERT INTO orders.program_order_metadata(tenant_id,order_id,profile_id,policy_revision_id,require_reference,external_reference,
    baseline_principal_id,baseline_device_id,baseline_key,baseline_fingerprint,baseline_observed_revision,bound_at)
VALUES(@tenant_id,@order_id,@profile_id,@policy_revision_id,false,NULL,@principal_id,@device_id,@key,@fingerprint,@expected_revision,@bound_at)
ON CONFLICT DO NOTHING RETURNING order_id;
