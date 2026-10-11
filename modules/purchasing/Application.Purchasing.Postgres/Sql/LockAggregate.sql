SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':purchasing:' || @family || ':' || @id::text, 0));
