INSERT INTO identity_access.account_lifecycle_receipts
    (changed_by_principal_id, idempotency_key, request_fingerprint, account_id, availability,
     revision, disabled_at, changed_by_device_id, reason, changed_at)
VALUES (@principal, @key, @fingerprint, @account, @availability, @revision, @disabled_at, @device, @reason, @changed_at);
