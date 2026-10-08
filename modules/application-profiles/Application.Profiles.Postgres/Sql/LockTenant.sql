SELECT pg_advisory_xact_lock(hashtextextended('profiles-routing:' || @tenant::text,0))
