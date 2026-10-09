SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':invoices:issue:' || @actor::text || ':' || @key,0));
