UPDATE profiles.policy_heads SET revision=@revision,require_reference=@required,require_approval=@approval WHERE tenant_id=@tenant AND revision=@expected
