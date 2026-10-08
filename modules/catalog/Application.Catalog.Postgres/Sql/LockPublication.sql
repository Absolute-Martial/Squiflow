-- Shared contract with Pricing and Orders PinCommercialPublication. UUID text is
-- canonical; MD5's first 64 bits provide a stable, versioned advisory key.
SELECT pg_advisory_xact_lock(
    ('x' || substr(md5('application:commercial-publication:v1:' || @tenant_id::uuid::text), 1, 16))::bit(64)::bigint);
