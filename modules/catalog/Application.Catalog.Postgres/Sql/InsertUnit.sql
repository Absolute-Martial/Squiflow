INSERT INTO catalog.units
    (tenant_id, id, code, name, precision, status, revision, created_by_account_id, created_at)
VALUES
    (@tenant_id, @id, @code, @name, @precision, 'active', 1, @account_id, @created_at);
