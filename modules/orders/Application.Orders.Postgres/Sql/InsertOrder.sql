INSERT INTO orders.order_drafts
    (tenant_id, id, created_by_account_id, summary, currency_code, total, revision, created_at,
     customer_organization_id, customer_program_id)
VALUES
    (@tenant_id, @id, @created_by_account_id, @summary, @currency_code, @total, @revision, @created_at,
     @customer_organization_id, @customer_program_id)
