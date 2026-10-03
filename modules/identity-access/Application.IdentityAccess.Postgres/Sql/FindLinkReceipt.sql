SELECT request_fingerprint, account_id, issuer, subject,
       linked_by_principal_id, linked_by_device_id, linked_at
FROM identity_access.identity_link_receipts
WHERE linked_by_principal_id = @principal_id AND idempotency_key = @key;
