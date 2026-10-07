INSERT INTO customers.individuals
    (tenant_id, id, display_name, email, phone, availability, revision,
     created_by_account_id, created_at, customer_type, external_registration_id,
     normalized_name, normalized_email, normalized_phone, normalized_external_registration_id,
     address_line1, address_line2, city, notes)
VALUES (@tenant_id, @id, @display_name, @email, @phone, 1, 1, @account_id, @created_at,
        @customer_type, @external_registration_id, @normalized_name, @normalized_email,
        @normalized_phone, @normalized_external_registration_id, @address_line1,
        @address_line2, @city, @notes)
