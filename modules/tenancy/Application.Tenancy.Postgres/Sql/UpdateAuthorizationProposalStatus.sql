UPDATE tenancy.tenant_authorization_proposals
SET status = @status,
    attempt_count = attempt_count + @attempt_increment,
    failure_code = @failure_code,
    updated_at = @occurred_at
WHERE tenant_id = @tenant_id AND proposal_id = @proposal_id;
