SELECT c.*
FROM customers.individuals original
JOIN customers.individuals c ON c.tenant_id = original.tenant_id
  AND c.id = COALESCE(original.redirect_target_individual_id, original.id)
WHERE original.tenant_id = @tenant_id AND original.id = @id;
