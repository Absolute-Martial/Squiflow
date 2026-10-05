SELECT revision
FROM tenancy.tenant_authorization_state
WHERE tenant_id = @tenant_id;
