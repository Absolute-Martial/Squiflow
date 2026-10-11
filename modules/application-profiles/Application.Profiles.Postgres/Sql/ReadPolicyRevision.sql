SELECT revision,require_reference,version,facts,require_approval FROM profiles.policy_revisions WHERE tenant_id=@tenant AND policy_id=@id
