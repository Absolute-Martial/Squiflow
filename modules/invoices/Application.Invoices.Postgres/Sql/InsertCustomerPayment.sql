INSERT INTO invoices.customer_payments(tenant_id,payment_id,debtor_kind,debtor_id,currency_code,amount,channel,evidence,external_reference,recorded_by_account_id,recorded_at,schema_version)
VALUES(@tenant,@payment,@kind,@debtor,@currency,@amount,@channel,@evidence,@reference,@actor,@at,1);
