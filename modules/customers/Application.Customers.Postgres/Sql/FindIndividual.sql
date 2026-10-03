SELECT id, display_name, email, phone, availability, revision,
       created_by_account_id, created_at, availability_changed_by_account_id, availability_changed_at
FROM customers.individuals
WHERE tenant_id = @tenant_id AND id = @individual_id
