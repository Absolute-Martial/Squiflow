INSERT INTO invoices.organization_ordinals(tenant_id,organization_id,last_ordinal) VALUES(@tenant,@organization,1)
ON CONFLICT(tenant_id,organization_id) DO UPDATE SET last_ordinal=invoices.organization_ordinals.last_ordinal+1
WHERE invoices.organization_ordinals.last_ordinal<9223372036854775807 RETURNING last_ordinal;
