SELECT tenant_id,payment_id,debtor_kind,debtor_id,currency_code,amount,channel,evidence,external_reference,
    recorded_by_account_id,recorded_at,revision,state,allocated,last_confirmation_evidence,valid_history
FROM invoices.customer_payment_balances WHERE tenant_id=@tenant AND payment_id=@payment;
