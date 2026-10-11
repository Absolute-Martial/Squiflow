SELECT fingerprint, individual_id AS id, display_name, email, phone, availability, revision,
       created_by_account_id, created_at, availability_changed_by_account_id, availability_changed_at,
       contact_changed_by_account_id, contact_changed_at,
       'individual'::varchar AS customer_type, NULL::varchar AS external_registration_id,
       NULL::varchar AS address_line1, NULL::varchar AS address_line2,
       NULL::varchar AS city, NULL::varchar AS notes, NULL::uuid AS redirect_target_individual_id
FROM customers.individual_command_receipts
WHERE tenant_id = @tenant_id AND account_id = @account_id
  AND operation = @operation AND idempotency_key = @key
