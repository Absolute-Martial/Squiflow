SELECT pg_advisory_xact_lock_shared(('x' || substr(md5('application:commercial-publication:v1:' || @tenant::text), 1, 16))::bit(64)::bigint);
