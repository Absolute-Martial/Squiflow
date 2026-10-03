INSERT INTO customers.organizations
    (tenant_id, id, created_by_account_id, display_name, created_at)
VALUES (@tenant_id, @id, @account_id, @display_name, @created_at)
