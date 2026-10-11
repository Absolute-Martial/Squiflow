SELECT pg_advisory_xact_lock(('x'||substr(md5('application:fulfillment-receipt:v1:'||@tenant::uuid::text||':'||@actor::uuid::text||':'||@operation::integer::text||':'||@key),1,16))::bit(64)::bigint);
