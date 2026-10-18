SELECT r.original,r.outstanding,p.unapplied,r.valid IS NOT FALSE AND p.valid IS NOT FALSE
FROM (
    SELECT coalesce(sum(original_amount),0) AS original,coalesce(sum(original_amount-allocated),0) AS outstanding,
        bool_and(valid_history) AS valid FROM invoices.receivable_balances
    WHERE tenant_id=@tenant AND debtor_kind=@kind AND debtor_id=@debtor
) r CROSS JOIN (
    SELECT coalesce(sum(CASE WHEN state=2 THEN amount-allocated ELSE 0 END),0) AS unapplied,
        bool_and(valid_history) AS valid FROM invoices.customer_payment_balances
    WHERE tenant_id=@tenant AND debtor_kind=@kind AND debtor_id=@debtor
) p;
