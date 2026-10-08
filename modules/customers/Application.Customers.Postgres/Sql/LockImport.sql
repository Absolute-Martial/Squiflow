WITH gate AS MATERIALIZED (
    SELECT pg_advisory_xact_lock(hashtextextended(@tenant_id::text || ':customer-import:' || @import_id::text, 0))
)
SELECT import_id FROM customers.imports, gate WHERE tenant_id = @tenant_id AND import_id = @import_id;
