INSERT INTO customers.individuals
    (tenant_id, id, display_name, email, phone, availability, revision,
     created_by_account_id, created_at, customer_type, normalized_name, normalized_email, normalized_phone)
VALUES (@tenant_id, @id, @display_name, @email, @phone, 1, 1, @account_id, @created_at,
        'individual', upper(btrim(@display_name)),
        CASE WHEN @email IS NULL THEN NULL ELSE upper(btrim(@email)) END,
        CASE WHEN @phone IS NULL THEN NULL ELSE regexp_replace(@phone, '[^0-9]', '', 'g') END)
