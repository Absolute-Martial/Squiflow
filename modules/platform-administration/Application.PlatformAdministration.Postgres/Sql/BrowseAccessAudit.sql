SELECT id, principal_id, device_id, operation, outcome, reason, occurred_at
FROM platform_administration.read_access_audit(@before_at, @before_id, @principal, @outcome, @limit)
ORDER BY occurred_at DESC, id DESC;
