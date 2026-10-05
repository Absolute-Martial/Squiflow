SELECT request_fingerprint, previous_owner_account_id, current_owner_account_id,
       result_tenant_revision, result_authorization_revision
FROM tenancy.owner_transfer_receipts
WHERE tenant_id = @tenant_id AND requested_by_account_id = @actor_account_id
  AND idempotency_key = @idempotency_key
FOR UPDATE;
