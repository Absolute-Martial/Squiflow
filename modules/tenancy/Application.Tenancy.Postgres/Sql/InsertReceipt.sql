INSERT INTO tenancy.tenant_provisioning_receipts
    (provisioned_by_principal_id, idempotency_key, request_fingerprint,
     tenant_id, display_name, provisioned_by_device_id, activated_at)
VALUES (@principal_id, @idempotency_key, @fingerprint,
        @tenant_id, @display_name, @device_id, @activated_at)
ON CONFLICT (provisioned_by_principal_id, idempotency_key) DO NOTHING
RETURNING tenant_id
