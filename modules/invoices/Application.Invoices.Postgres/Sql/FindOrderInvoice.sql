SELECT invoice_id FROM invoices.issued_invoices WHERE tenant_id=@tenant AND source_order_id=@order;
