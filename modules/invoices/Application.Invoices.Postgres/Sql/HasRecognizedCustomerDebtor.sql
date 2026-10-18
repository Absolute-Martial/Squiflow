SELECT CASE WHEN bool_and(valid_history IS TRUE) IS FALSE THEN NULL ELSE count(*)>0 END
FROM invoices.receivable_balances WHERE tenant_id=@tenant AND debtor_kind=@kind AND debtor_id=@debtor;
