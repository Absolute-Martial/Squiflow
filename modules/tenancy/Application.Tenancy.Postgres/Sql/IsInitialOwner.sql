SELECT EXISTS (
    SELECT 1
    FROM tenancy.memberships AS membership
    JOIN tenancy.tenants AS tenant ON tenant.id=membership.tenant_id
    JOIN identity_access.accounts AS account ON account.id=membership.account_id
    WHERE membership.tenant_id = @tenant_id
      AND membership.account_id = @account_id
      AND membership.availability = 1
      AND membership.is_initial_owner
      AND tenant.availability = 1
      AND account.availability = 1
);
