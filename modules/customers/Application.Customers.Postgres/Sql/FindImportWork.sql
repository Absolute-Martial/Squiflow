SELECT work_id, import_id, status, created_at, completed_at, last_error, fingerprint, authorization_revision, account_id
FROM customers.import_work
WHERE tenant_id = @tenant_id AND import_id = @import_id;
