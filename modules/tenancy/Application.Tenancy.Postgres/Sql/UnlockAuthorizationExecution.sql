SELECT pg_advisory_unlock(hashtextextended(@tenant_id::text || ':tenant-authorization-execution',0));
