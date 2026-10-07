INSERT INTO customers.import_rows
    (tenant_id,import_id,row_number,source_row_hash,row_id,name,customer_type,external_id,email,phone,
     address_line1,address_line2,city,notes,status,error_code,error_message,requires_decision,attempts,
     name_signal,email_signal,phone_signal,external_id_signal)
SELECT @tenant_id,@import_id,r.row_number,r.source_row_hash,r.row_id,r.name,r.customer_type,r.external_id,r.email,r.phone,
    r.address_line1,r.address_line2,r.city,r.notes,r.status,r.error_code,r.error_message,false,0,
    r.name_signal,r.email_signal,r.phone_signal,r.external_id_signal
FROM jsonb_to_recordset(@rows) AS r(row_number int,source_row_hash text,row_id uuid,name text,customer_type text,
    external_id text,email text,phone text,address_line1 text,address_line2 text,city text,notes text,status int,
    error_code text,error_message text,name_signal text,email_signal text,phone_signal text,external_id_signal text);
