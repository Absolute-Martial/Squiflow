UPDATE customers.duplicate_cases
SET outcome = @outcome, resolved_by_account_id = @account_id, resolved_at = @resolved_at, reason = @reason
WHERE tenant_id = @tenant_id AND case_id = @case_id AND (outcome IS NULL OR outcome = 1)
RETURNING case_id;
