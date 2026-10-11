UPDATE customers.import_rows r SET decision=d.decision,mapping_customer_id=d.customer_id
FROM jsonb_to_recordset(@decisions) AS d(row_number int,decision int,customer_id uuid)
WHERE r.tenant_id=@tenant_id AND r.import_id=@import_id AND r.row_number=d.row_number AND r.decision IS NULL;
