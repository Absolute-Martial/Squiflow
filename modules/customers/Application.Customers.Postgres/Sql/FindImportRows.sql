SELECT row_number, source_row_hash, name, customer_type, external_id, email, phone,
       address_line1, address_line2, city, notes, status, error_code, error_message,
       customer_id, processed_at, row_id, requires_decision, duplicate_evidence, decision, mapping_customer_id, attempts
FROM customers.import_rows
WHERE tenant_id = @tenant_id AND import_id = @import_id AND row_number > @after_row
ORDER BY row_number LIMIT @limit;
