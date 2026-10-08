INSERT INTO customers.import_source_objects
    (tenant_id, object_key, provider_scope, byte_length, sha256, content_type, state, retention,
     created_at, expires_at, failure_code)
VALUES (@tenant_id, @object_key, @provider_scope, @byte_length, @sha256, @content_type, 1, @retention,
        @created_at, @expires_at, NULL)
ON CONFLICT DO NOTHING;
