SELECT resolution_id, customer_id, other_customer_id, outcome, account_id, resolved_at, reason, evidence
FROM customers.duplicate_command_receipts
WHERE tenant_id = @tenant_id AND (customer_id = @customer_id OR other_customer_id = @customer_id)
  AND (@after_id IS NULL OR resolution_id > @after_id)
ORDER BY resolution_id LIMIT @limit;
