SELECT availability, revision
FROM tenancy.memberships
WHERE tenant_id = @tenant_id AND account_id = @account_id
FOR UPDATE;
