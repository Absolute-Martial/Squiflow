SELECT id, revision, redirect_target_individual_id, availability
FROM customers.individuals
WHERE tenant_id = @tenant_id AND id IN (@first_id, @second_id)
ORDER BY id FOR UPDATE;
