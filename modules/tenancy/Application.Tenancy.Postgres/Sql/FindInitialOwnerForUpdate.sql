SELECT account_id, availability, revision
FROM tenancy.memberships
WHERE tenant_id = @tenant_id AND is_initial_owner
FOR UPDATE;
