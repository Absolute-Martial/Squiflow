INSERT INTO customers.import_work
    (tenant_id, work_id, import_id, account_id, idempotency_key, fingerprint, status, created_at, authorization_revision, generation)
VALUES (@tenant_id, @work_id, @import_id, @account_id, @key, @fingerprint, 1, @created_at, @authority_revision, 0);
