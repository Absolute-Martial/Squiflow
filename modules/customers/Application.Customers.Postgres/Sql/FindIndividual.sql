SELECT id, display_name, email, phone, availability, revision,
       created_by_account_id, created_at, availability_changed_by_account_id, availability_changed_at,
       contact_changed_by_account_id, contact_changed_at, customer_type, external_registration_id,
       address_line1, address_line2, city, notes, redirect_target_individual_id
FROM customers.individuals
WHERE tenant_id = @tenant_id AND id = @individual_id
