SELECT tenant_id,id,organization_id,display_name FROM customers.programs
WHERE tenant_id=@tenant_id AND id=@id;
