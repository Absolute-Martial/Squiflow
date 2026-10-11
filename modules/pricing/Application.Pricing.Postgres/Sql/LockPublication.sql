-- Identical stable key to Catalog LockPublication and Orders PinCommercialPublication. This lock
-- precedes receipt/key/family/row locks in every publication mutation.
SELECT pg_advisory_xact_lock(
    ('x' || substr(md5('application:commercial-publication:v1:' || @tenant_id::uuid::text), 1, 16))::bit(64)::bigint);
