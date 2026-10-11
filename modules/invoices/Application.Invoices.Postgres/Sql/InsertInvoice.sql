INSERT INTO invoices.issued_invoices(tenant_id,invoice_id,source_order_id,source_order_revision,organization_id,
 program_id,organization_ordinal,currency_code,total,line_count,debtor,issued_by_account_id,issued_at,business_date,schema_version)
VALUES(@tenant,@invoice,@order,@revision,@organization,@program,@ordinal,@currency,@total,@count,
 @debtor::jsonb,@actor,@at,@date,1);
