SELECT pg_advisory_lock(hashtextextended(@tenant_id::text || ':tenant-authorization-execution',0));
