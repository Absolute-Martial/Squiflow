INSERT INTO identity_access.identity_link_receipts
    (linked_by_principal_id, idempotency_key, request_fingerprint,
     account_id, issuer, subject, linked_by_device_id, linked_at)
VALUES
    (@principal_id, @idempotency_key, @fingerprint,
     @account_id, @issuer, @subject, @device_id, @linked_at);
