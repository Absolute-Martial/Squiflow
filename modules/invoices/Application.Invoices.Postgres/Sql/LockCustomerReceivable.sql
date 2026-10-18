SELECT pg_advisory_xact_lock(hashtextextended('customer-receivable:'||@tenant::text||':'||@invoice::text,0));
