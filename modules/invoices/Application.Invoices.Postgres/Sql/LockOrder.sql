SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':invoices:order:' || @order::text,0));
