SELECT tenant_id,id,display_name,revision,availability FROM customers.individuals
WHERE tenant_id=@tenant_id AND id=@id AND redirect_target_individual_id IS NULL
FOR SHARE;
