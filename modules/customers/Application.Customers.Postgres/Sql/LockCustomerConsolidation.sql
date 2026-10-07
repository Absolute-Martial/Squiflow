SELECT id, revision, redirect_target_individual_id, availability
FROM customers.individuals
WHERE tenant_id = @tenant_id
  AND (id IN (@source_id, @canonical_id) OR redirect_target_individual_id = @source_id)
ORDER BY id FOR UPDATE;
