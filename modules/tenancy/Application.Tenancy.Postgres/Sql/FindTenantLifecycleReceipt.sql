SELECT request_fingerprint, operation, tenant_id, result_availability,
       result_revision, suspended_at
FROM tenancy.tenant_lifecycle_receipts
WHERE changed_by_principal_id = @principal_id AND idempotency_key = @key;
