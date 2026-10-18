INSERT INTO invoices.receivables(tenant_id,invoice_id,debtor_kind,debtor_id,currency_code,original_amount,invoice_date,schema_version)
VALUES(@tenant,@invoice,@kind,@debtor,@currency,@amount,@date,1);
