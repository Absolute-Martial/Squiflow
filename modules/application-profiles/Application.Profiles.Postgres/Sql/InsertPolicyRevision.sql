INSERT INTO profiles.policy_revisions(tenant_id,policy_id,revision,require_reference,version,facts) VALUES(@tenant,@id,@revision,@required,1,@facts::jsonb)
