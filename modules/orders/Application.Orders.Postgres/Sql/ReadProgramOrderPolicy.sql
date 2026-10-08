SELECT profile_id,policy_revision_id,require_reference,external_reference,
    COALESCE(baseline_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid
        AND baseline_device_id <> '00000000-0000-0000-0000-000000000000'::uuid
        AND char_length(baseline_key) BETWEEN 1 AND 128
        AND baseline_fingerprint ~ '^[0-9a-f]{64}$'
        AND baseline_observed_revision > 0 AND NOT require_reference, false)
FROM orders.program_order_metadata WHERE tenant_id=@tenant_id AND order_id=@order_id;
