SELECT id, code, name, precision, status, revision, created_by_account_id, created_at,
       retired_at, retired_by_account_id
FROM catalog.units
WHERE tenant_id = @tenant_id AND id = @unit_id
FOR SHARE;
