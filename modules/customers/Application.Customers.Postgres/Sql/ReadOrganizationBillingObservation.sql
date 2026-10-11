SELECT tenant_id,id,display_name FROM customers.organizations
WHERE tenant_id=@tenant_id AND id=@id;
