INSERT INTO identity_access.account_onboarding_receipts
    (provisioned_by_principal_id, idempotency_key, request_fingerprint,
     account_id, issuer, subject, provisioned_by_device_id, created_at)
VALUES
    (@principal_id, @idempotency_key, @fingerprint,
     @account_id, @issuer, @subject, @device_id, @created_at);
