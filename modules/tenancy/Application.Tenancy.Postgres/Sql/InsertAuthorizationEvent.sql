INSERT INTO tenancy.tenant_authorization_events(
    event_id, proposal_id, tenant_id, actor_account_id,
    status, reason, authorization_revision, occurred_at)
VALUES (
    @event_id, @proposal_id, @tenant_id, @actor_account_id,
    @status, @reason, @authorization_revision, @occurred_at);
