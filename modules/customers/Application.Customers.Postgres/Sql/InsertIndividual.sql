INSERT INTO customers.individuals
    (tenant_id, id, display_name, email, phone, availability, revision,
     created_by_account_id, created_at)
VALUES (@tenant_id, @id, @display_name, @email, @phone, 1, 1, @account_id, @created_at)
