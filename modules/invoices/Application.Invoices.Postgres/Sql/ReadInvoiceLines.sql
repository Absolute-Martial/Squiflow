SELECT position,description,quantity,unit_code,unit_price,line_total FROM invoices.issued_lines
WHERE tenant_id=@tenant AND invoice_id=@invoice ORDER BY position LIMIT 101;
