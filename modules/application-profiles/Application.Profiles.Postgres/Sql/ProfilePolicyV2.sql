ALTER TABLE profiles.policy_heads ADD COLUMN require_approval boolean NOT NULL DEFAULT false;
ALTER TABLE profiles.policy_revisions ADD COLUMN require_approval boolean NOT NULL DEFAULT false;
ALTER TABLE profiles.policy_revisions DROP CONSTRAINT policy_revisions_version_check;
ALTER TABLE profiles.policy_revisions ADD CONSTRAINT policy_revisions_version_check CHECK (version IN (1,2));
ALTER TABLE profiles.publications DROP CONSTRAINT publications_version_check;
ALTER TABLE profiles.publications ADD CONSTRAINT publications_version_check CHECK (version IN (1,2));

CREATE OR REPLACE FUNCTION profiles.guard_policy_head() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='INSERT' THEN
        IF NEW.revision<>1 OR NEW.published_policy_id IS NOT NULL THEN
            RAISE EXCEPTION 'Initial policy heads must be explicit unpublished drafts' USING ERRCODE='23514';
        END IF;
        RETURN NEW;
    END IF;
    IF TG_OP='DELETE' OR NEW.tenant_id<>OLD.tenant_id OR NEW.revision<>OLD.revision+1 THEN
        RAISE EXCEPTION 'Policy heads require one revision advance and retained tenant identity' USING ERRCODE='23514';
    END IF;
    IF NEW.published_policy_id IS DISTINCT FROM OLD.published_policy_id AND NOT EXISTS (
        SELECT 1 FROM profiles.policy_revisions p WHERE p.tenant_id=NEW.tenant_id AND p.policy_id=NEW.published_policy_id
            AND p.revision=NEW.revision AND p.require_reference=NEW.require_reference AND p.require_approval=NEW.require_approval) THEN
        RAISE EXCEPTION 'Policy publication must retain the exact current typed settings' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION profiles.guard_authority() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='INSERT' THEN
        IF NEW.revision<>1 OR NEW.active_profile_id IS NOT NULL OR NEW.legacy_baseline_profile_id IS NOT NULL THEN
            RAISE EXCEPTION 'Initial profile authority must be unpublished routing state' USING ERRCODE='23514';
        END IF;
        RETURN NEW;
    END IF;
    IF TG_OP='DELETE' OR NEW.tenant_id<>OLD.tenant_id OR NEW.revision<>OLD.revision+1
        OR OLD.legacy_baseline_profile_id IS NOT NULL AND NEW.legacy_baseline_profile_id IS DISTINCT FROM OLD.legacy_baseline_profile_id THEN
        RAISE EXCEPTION 'Profile authority requires one revision advance and a retained legacy baseline' USING ERRCODE='23514';
    END IF;
    IF NEW.legacy_baseline_profile_id IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM profiles.publications p JOIN profiles.policy_revisions s ON s.tenant_id=p.tenant_id AND s.policy_id=p.policy_id
        WHERE p.tenant_id=NEW.tenant_id AND p.profile_id=NEW.legacy_baseline_profile_id AND p.legacy_baseline
            AND NOT s.require_reference AND NOT s.require_approval) THEN
        RAISE EXCEPTION 'Legacy baseline must be an explicit optional retained profile' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
