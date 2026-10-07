INSERT INTO customers.customer_redirects
    (tenant_id, source_customer_id, canonical_customer_id, created_by_account_id, created_at)
VALUES (@tenant_id, @source_id, @canonical_id, @account_id, @created_at)
ON CONFLICT (tenant_id, source_customer_id) DO NOTHING
RETURNING 1;
