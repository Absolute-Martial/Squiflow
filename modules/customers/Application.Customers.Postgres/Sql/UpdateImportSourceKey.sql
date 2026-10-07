UPDATE customers.imports
SET source_object_key = @object_key
WHERE tenant_id = @tenant_id AND import_id = @import_id;
