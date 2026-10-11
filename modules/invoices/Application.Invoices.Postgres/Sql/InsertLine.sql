INSERT INTO invoices.issued_lines(tenant_id,invoice_id,position,description,quantity,unit_code,unit_price,line_total)
VALUES(@tenant,@invoice,@position,@description,@quantity,@unit,@price,@total);
