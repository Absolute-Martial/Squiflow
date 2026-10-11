SELECT invoice_id,source_order_id,source_order_revision,organization_id,program_id,organization_ordinal,
 currency_code,total,line_count,debtor::text,issued_by_account_id,issued_at,business_date,schema_version
FROM invoices.issued_invoices WHERE tenant_id=@tenant AND invoice_id=@invoice;
