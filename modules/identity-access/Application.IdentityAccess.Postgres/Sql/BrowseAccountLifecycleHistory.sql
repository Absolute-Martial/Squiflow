SELECT availability, revision, disabled_at, changed_by_principal_id,
       changed_by_device_id, reason, changed_at
FROM identity_access.account_lifecycle_receipts
WHERE account_id = @account AND (@before IS NULL OR revision < @before)
ORDER BY revision DESC LIMIT @limit;
