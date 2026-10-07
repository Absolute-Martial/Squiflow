SELECT id, organization_id, program_id, individual_id, availability, revision,
       created_by_account_id, created_at, changed_by_account_id, changed_at
FROM customers.representatives
WHERE tenant_id = @tenant_id
  AND organization_id = @organization_id
  AND individual_id = @individual_id
  AND availability = 1
  AND ((@program_id IS NULL AND program_id IS NULL) OR program_id = @program_id)
