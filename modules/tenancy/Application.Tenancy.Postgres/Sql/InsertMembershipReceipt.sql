INSERT INTO tenancy.membership_lifecycle_receipts
    (changed_by_principal_id, idempotency_key, request_fingerprint, operation,
     tenant_id, account_id, result_status, result_availability, result_revision,
     invited_at, activated_at, suspended_at, removed_at,
     is_initial_owner, changed_by_device_id, occurred_at)
VALUES
    (@principal_id, @key, @fingerprint, @operation,
     @tenant_id, @account_id, @status, @availability, @revision,
     @invited_at, @activated_at, @suspended_at, @removed_at,
     @is_initial_owner, @device_id, @occurred_at);
