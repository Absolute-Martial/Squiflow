INSERT INTO customers.programs
    (tenant_id, id, organization_id, created_by_account_id, display_name, created_at)
VALUES (@tenant_id, @id, @organization_id, @account_id, @display_name, @created_at)
