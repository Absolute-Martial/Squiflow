UPDATE customers.import_source_objects SET state=@state,failure_code=@failure_code
WHERE tenant_id=@tenant_id AND object_key=@object_key AND state IN(1,2,4,5);
