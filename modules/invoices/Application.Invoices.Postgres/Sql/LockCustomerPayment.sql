SELECT pg_advisory_xact_lock(hashtextextended('customer-payment:'||@tenant::text||':'||@payment::text,0));
