SELECT policy_id,legacy_baseline,version,facts FROM profiles.publications WHERE tenant_id=@tenant AND profile_id=@id
