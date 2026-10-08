WITH active_individual AS (
    SELECT 1
    FROM customers.individuals
    WHERE tenant_id = @tenant_id
      AND id = @individual_id
       AND availability = 1
       AND redirect_target_individual_id IS NULL
    FOR SHARE
)
INSERT INTO customers.representatives
    (tenant_id, id, organization_id, program_id, individual_id, availability, revision,
     created_by_account_id, created_at, changed_by_account_id, changed_at)
SELECT @tenant_id, @representative_id, @organization_id, @program_id, @individual_id, 1, 1,
       @account_id, @created_at, NULL, NULL
FROM active_individual
ON CONFLICT DO NOTHING
RETURNING id, organization_id, program_id, individual_id, availability, revision,
          created_by_account_id, created_at, changed_by_account_id, changed_at
