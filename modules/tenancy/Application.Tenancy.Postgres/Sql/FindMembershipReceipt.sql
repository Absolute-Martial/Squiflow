SELECT request_fingerprint, result_status, tenant_id, account_id,
       result_availability, result_revision, invited_at, activated_at,
       suspended_at, removed_at, is_initial_owner
FROM tenancy.membership_lifecycle_receipts
WHERE changed_by_principal_id = @principal_id AND idempotency_key = @key;
