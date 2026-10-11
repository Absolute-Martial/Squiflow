SELECT id, organization_id, program_id, individual_id, availability, revision,
       created_by_account_id, created_at, changed_by_account_id, changed_at
FROM customers.representatives
WHERE tenant_id = @tenant_id AND id = @representative_id
