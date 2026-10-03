SELECT request_fingerprint, tenant_id, display_name,
       provisioned_by_principal_id, provisioned_by_device_id, activated_at
FROM tenancy.tenant_provisioning_receipts
WHERE provisioned_by_principal_id = @principal_id
  AND idempotency_key = @idempotency_key
