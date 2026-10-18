SELECT tenant_id,invoice_id,debtor_kind,debtor_id,currency_code,original_amount,invoice_date,recognized_at,allocated,valid_history
FROM invoices.receivable_balances WHERE tenant_id=@tenant AND invoice_id=@invoice;
