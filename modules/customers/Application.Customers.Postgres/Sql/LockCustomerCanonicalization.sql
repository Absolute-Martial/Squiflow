-- One tenant-scoped gate precedes every consolidation/representative row lock.
-- The additive database triggers use the identical namespace/key for direct writes.
SELECT pg_advisory_xact_lock(hashtextextended('customers:canonical:' || @tenant_id::text, 0));
