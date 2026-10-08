UPDATE customers.object_storage_usage
SET reserved_bytes = reserved_bytes + @byte_length, updated_at = @now
WHERE tenant_id = @tenant_id AND provider_scope = @provider_scope;
