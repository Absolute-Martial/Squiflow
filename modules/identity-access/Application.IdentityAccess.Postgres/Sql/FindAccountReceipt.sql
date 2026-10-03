SELECT request_fingerprint, account_id, issuer, subject,
       provisioned_by_principal_id, provisioned_by_device_id, created_at
FROM identity_access.account_onboarding_receipts
WHERE provisioned_by_principal_id = @principal_id AND idempotency_key = @key;
