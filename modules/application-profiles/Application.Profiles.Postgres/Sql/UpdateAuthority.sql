UPDATE profiles.authority SET revision=@revision,active_profile_id=@active,legacy_baseline_profile_id=@baseline WHERE tenant_id=@tenant AND revision=@expected
