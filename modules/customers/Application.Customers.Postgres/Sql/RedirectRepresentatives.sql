UPDATE customers.representatives s SET availability = 2, revision = revision + 1,
    changed_by_account_id = @account_id, changed_at = @changed_at
WHERE s.tenant_id = @tenant_id AND s.individual_id = @source_id AND s.availability = 1
  AND EXISTS (SELECT 1 FROM customers.representatives c
      WHERE c.tenant_id = s.tenant_id AND c.individual_id = @canonical_id AND c.availability = 1
        AND c.organization_id = s.organization_id AND c.program_id IS NOT DISTINCT FROM s.program_id);
UPDATE customers.representatives SET individual_id = @canonical_id, revision = revision + 1,
    changed_by_account_id = @account_id, changed_at = @changed_at
WHERE tenant_id = @tenant_id AND individual_id = @source_id AND availability = 1;
