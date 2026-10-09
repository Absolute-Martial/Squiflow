INSERT INTO profiles.policy_revisions(tenant_id,policy_id,revision,require_reference,require_approval,version,facts) VALUES(@tenant,@id,@revision,@required,@approval,2,@facts::jsonb)
