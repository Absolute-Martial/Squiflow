SELECT id FROM customers.individuals WHERE tenant_id = @tenant_id AND id = @id
  AND redirect_target_individual_id IS NULL AND availability = 1 FOR SHARE;
