INSERT INTO profiles.publications(tenant_id,profile_id,policy_id,legacy_baseline,version,facts) VALUES(@tenant,@id,@policy,@baseline,1,@facts::jsonb)
