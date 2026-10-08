UPDATE customers.import_rows SET status=@status,customer_id=@customer_id,error_code=@error_code,
    error_message=@error_message,attempts=attempts+1,processed_at=clock_timestamp()
WHERE tenant_id=@tenant_id AND import_id=@import_id AND row_number=@row_number AND status IN (1,5);
