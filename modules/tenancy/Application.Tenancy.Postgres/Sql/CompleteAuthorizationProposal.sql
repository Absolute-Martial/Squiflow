UPDATE tenancy.tenant_authorization_state
SET revision = @revision, updated_at = @occurred_at
WHERE tenant_id = @tenant_id;

UPDATE tenancy.tenant_authorization_proposals
SET status = 2,
    applied_authorization_revision = @revision,
    failure_code = NULL,
    updated_at = @occurred_at
WHERE tenant_id = @tenant_id AND proposal_id = @proposal_id;
