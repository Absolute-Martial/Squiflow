SELECT tenant_id,invoice_id,debtor_kind,debtor_id,currency_code,original_amount,invoice_date,recognized_at,allocated,valid_history
FROM invoices.receivable_balances WHERE tenant_id=@tenant
    AND (@kind=0 OR (debtor_kind=@kind AND debtor_id=@debtor::uuid))
    AND (@after::uuid IS NULL OR invoice_id>@after::uuid)
ORDER BY invoice_id LIMIT 51;
