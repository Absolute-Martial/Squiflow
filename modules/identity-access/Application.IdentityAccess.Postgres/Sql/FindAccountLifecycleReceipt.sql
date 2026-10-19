SELECT request_fingerprint, account_id, availability, revision, disabled_at,
       changed_by_principal_id, changed_by_device_id, reason, changed_at
FROM identity_access.account_lifecycle_receipts
WHERE changed_by_principal_id = @principal AND idempotency_key = @key;
