SELECT r.fingerprint, o.id, o.display_name, o.created_at
FROM customers.organization_receipts r
JOIN customers.organizations o ON o.tenant_id = r.tenant_id AND o.id = r.organization_id
WHERE r.tenant_id = @tenant_id AND r.account_id = @account_id AND r.idempotency_key = @key
