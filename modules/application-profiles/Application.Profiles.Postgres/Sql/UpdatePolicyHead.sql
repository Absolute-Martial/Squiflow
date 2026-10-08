UPDATE profiles.policy_heads SET revision=@revision,require_reference=@required WHERE tenant_id=@tenant AND revision=@expected
