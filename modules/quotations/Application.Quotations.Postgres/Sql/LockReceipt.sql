SELECT pg_advisory_xact_lock(('x' || substr(md5('application:quotation-command:v1:' ||
    @tenant::text || ':' || @actor::text || ':' || @operation || ':' || @key), 1, 16))::bit(64)::bigint);
