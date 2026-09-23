INSERT INTO orders.order_drafts
    (tenant_id, id, created_by_account_id, summary, currency_code, total, revision, created_at)
VALUES
    (@tenant_id, @id, @created_by_account_id, @summary, @currency_code, @total, @revision, @created_at)
