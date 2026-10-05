UPDATE tenancy.memberships
SET is_initial_owner = false, revision = revision + 1
WHERE tenant_id = @tenant_id AND account_id = @old_owner;
UPDATE tenancy.memberships
SET is_initial_owner = true, revision = revision + 1
WHERE tenant_id = @tenant_id AND account_id = @new_owner;
UPDATE tenancy.tenants
SET revision = @tenant_revision
WHERE id = @tenant_id;
UPDATE tenancy.tenant_authorization_state
SET revision = @authorization_revision, updated_at = @occurred_at
WHERE tenant_id = @tenant_id;
INSERT INTO tenancy.owner_transfer_receipts(
    tenant_id, requested_by_account_id, idempotency_key, request_fingerprint,
    previous_owner_account_id, current_owner_account_id,
    result_tenant_revision, result_authorization_revision, occurred_at)
VALUES (
    @tenant_id, @actor_account_id, @idempotency_key, @fingerprint,
    @old_owner, @new_owner, @tenant_revision, @authorization_revision, @occurred_at);
