SELECT EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=@tenant_id AND
    (normalized_name=@name OR normalized_email=@email OR normalized_phone=@phone
     OR normalized_external_registration_id=@external_id));
