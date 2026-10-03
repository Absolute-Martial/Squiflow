SELECT r.fingerprint, p.id, p.organization_id, p.display_name, p.created_at
FROM customers.program_receipts r
JOIN customers.programs p ON p.tenant_id = r.tenant_id AND p.id = r.program_id
WHERE r.tenant_id = @tenant_id AND r.account_id = @account_id AND r.idempotency_key = @key
