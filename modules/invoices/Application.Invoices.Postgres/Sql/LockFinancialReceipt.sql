SELECT pg_advisory_xact_lock(hashtextextended('customer-financial-receipt:'||@tenant::text||':'||@actor::text||':'||@key,0));
