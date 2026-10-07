UPDATE customers.import_source_objects
SET state = 3, failure_code = NULL
WHERE tenant_id = @tenant_id AND object_key = @object_key AND state NOT IN (6, 7);
