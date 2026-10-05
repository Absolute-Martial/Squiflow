SELECT EXISTS (
    SELECT 1 FROM tenancy.tenant_authorization_proposals
    WHERE tenant_id = @tenant_id AND status IN (1,4)
);
