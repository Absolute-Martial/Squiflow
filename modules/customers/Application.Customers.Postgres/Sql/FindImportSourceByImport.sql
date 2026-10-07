SELECT source.object_key, source.provider_scope, source.byte_length, source.sha256, source.content_type,
       source.state, source.retention, source.created_at, source.expires_at, source.failure_code
FROM customers.imports AS import
JOIN customers.import_source_objects AS source
  ON source.tenant_id = import.tenant_id AND source.object_key = import.source_object_key
 WHERE import.tenant_id = @tenant_id AND import.import_id = @import_id
FOR UPDATE OF source;
