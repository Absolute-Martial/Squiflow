SELECT proposal_id, tenant_id, requested_by_account_id, idempotency_key, request_fingerprint,
       kind, status, expected_authorization_revision, applied_authorization_revision,
       target_account_id, permission_id, role_id, expected_role_revision, role_name,
       requested_permissions, applied_permissions, attempt_count, failure_code,
       requested_at, updated_at
FROM tenancy.tenant_authorization_proposals
WHERE tenant_id = @tenant_id AND proposal_id = @proposal_id;
