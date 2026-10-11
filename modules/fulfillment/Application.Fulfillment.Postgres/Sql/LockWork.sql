SELECT pg_advisory_xact_lock(('x'||substr(md5('application:fulfillment-work:v1:'||@tenant::uuid::text||':'||@order::uuid::text),1,16))::bit(64)::bigint);
