-- Identical key to Catalog/Pricing LockPublication. This shared pin belongs to
-- the Orders effect/receipt transaction, so losing it also rolls back the effect.
SELECT pg_advisory_xact_lock_shared(
    ('x' || substr(md5('application:commercial-publication:v1:' || @tenant_id::uuid::text), 1, 16))::bit(64)::bigint);
