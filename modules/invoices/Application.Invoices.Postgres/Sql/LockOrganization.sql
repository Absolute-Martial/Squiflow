SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':invoices:organization:' || @organization::text,0));
