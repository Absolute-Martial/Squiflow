INSERT INTO tenancy.tenant_lifecycle_receipts
    (changed_by_principal_id, idempotency_key, request_fingerprint, operation,
     tenant_id, result_availability, result_revision, suspended_at,
     changed_by_device_id, occurred_at)
VALUES
    (@principal_id, @key, @fingerprint, @operation,
     @tenant_id, @availability, @revision, @suspended_at,
     @device_id, @occurred_at);
