UPDATE customers.representatives
SET availability = 2, revision = revision + 1,
    changed_by_account_id = @account_id, changed_at = @changed_at
WHERE tenant_id = @tenant_id AND organization_id = @organization_id AND id = @representative_id
  AND revision = @expected_revision AND availability = 1
RETURNING id, organization_id, program_id, individual_id, availability, revision,
          created_by_account_id, created_at, changed_by_account_id, changed_at
