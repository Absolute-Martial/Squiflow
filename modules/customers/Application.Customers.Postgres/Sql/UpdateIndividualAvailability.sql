UPDATE customers.individuals
SET availability = @availability, revision = revision + 1,
    availability_changed_by_account_id = @account_id, availability_changed_at = @changed_at
WHERE tenant_id = @tenant_id AND id = @individual_id AND revision = @expected_revision
  AND redirect_target_individual_id IS NULL
  AND availability <> @availability
RETURNING id, display_name, email, phone, availability, revision,
          created_by_account_id, created_at, availability_changed_by_account_id, availability_changed_at,
          contact_changed_by_account_id, contact_changed_at, customer_type, external_registration_id,
          address_line1, address_line2, city, notes, redirect_target_individual_id
