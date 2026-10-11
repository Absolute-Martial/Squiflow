INSERT INTO customers.duplicate_cases
    (tenant_id, case_id, customer_id, other_customer_id, evidence)
VALUES (@tenant_id, @case_id, @customer_id, @other_customer_id, @evidence)
ON CONFLICT (tenant_id, case_id) DO UPDATE
SET evidence = EXCLUDED.evidence
WHERE customers.duplicate_cases.outcome IS NULL;
