SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':purchasing:' || @family || ':' || @actor::text || ':' || @operation::text || ':' || @key, 0));
