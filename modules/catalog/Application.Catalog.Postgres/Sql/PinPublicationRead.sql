-- Shared with Catalog/Pricing/Orders commercial-publication:v1 locks. A fulfillment source read
-- holds this pin through the caller transaction so item classification cannot race publication.
SELECT pg_advisory_xact_lock_shared(
    ('x' || substr(md5('application:commercial-publication:v1:' || @tenant_id::uuid::text), 1, 16))::bit(64)::bigint);
