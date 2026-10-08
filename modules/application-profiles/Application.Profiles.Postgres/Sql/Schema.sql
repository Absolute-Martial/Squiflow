CREATE SCHEMA IF NOT EXISTS profiles;
CREATE TABLE profiles.policy_heads (
    tenant_id uuid PRIMARY KEY REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
    revision bigint NOT NULL CHECK (revision > 0),
    require_reference boolean NOT NULL,
    published_policy_id uuid NULL
);
CREATE TABLE profiles.policy_revisions (
    tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
    policy_id uuid NOT NULL,
    revision bigint NOT NULL CHECK (revision > 0),
    require_reference boolean NOT NULL,
    version integer NOT NULL CHECK (version = 1),
    facts jsonb NOT NULL CHECK (octet_length(facts::text) <= 8192),
    PRIMARY KEY (tenant_id, policy_id),
    UNIQUE (tenant_id, revision)
);
ALTER TABLE profiles.policy_heads ADD FOREIGN KEY (tenant_id,published_policy_id) REFERENCES profiles.policy_revisions(tenant_id,policy_id) ON DELETE RESTRICT;
CREATE TABLE profiles.publications (
    tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
    profile_id uuid NOT NULL,
    policy_id uuid NOT NULL,
    legacy_baseline boolean NOT NULL,
    version integer NOT NULL CHECK (version = 1),
    facts jsonb NOT NULL CHECK (octet_length(facts::text) <= 16384),
    PRIMARY KEY (tenant_id,profile_id),
    FOREIGN KEY (tenant_id,policy_id) REFERENCES profiles.policy_revisions(tenant_id,policy_id) ON DELETE RESTRICT
);
CREATE TABLE profiles.authority (
    tenant_id uuid PRIMARY KEY REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
    revision bigint NOT NULL CHECK (revision > 0),
    active_profile_id uuid NULL,
    legacy_baseline_profile_id uuid NULL,
    FOREIGN KEY (tenant_id,active_profile_id) REFERENCES profiles.publications(tenant_id,profile_id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id,legacy_baseline_profile_id) REFERENCES profiles.publications(tenant_id,profile_id) ON DELETE RESTRICT
);
CREATE TABLE profiles.command_receipts (
    tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
    actor_id uuid NOT NULL,
    actor_kind text NOT NULL CHECK (actor_kind IN ('tenant','platform')),
    device_id uuid NULL,
    operation text NOT NULL CHECK (operation IN ('edit-policy','publish-policy','publish-profile','activate-profile','select-legacy-baseline')),
    idempotency_key text NOT NULL CHECK (length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    version integer NOT NULL CHECK (version = 1),
    recorded_at timestamptz NOT NULL,
    observed_authorization_revision bigint NULL CHECK (observed_authorization_revision > 0),
    result jsonb NOT NULL CHECK (octet_length(result::text) <= 32768),
    PRIMARY KEY (tenant_id,actor_kind,actor_id,operation,idempotency_key),
    CHECK ((actor_kind='platform') = (device_id IS NOT NULL)),
    CHECK ((actor_kind='tenant') = (observed_authorization_revision IS NOT NULL))
);
CREATE FUNCTION profiles.reject_retained_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Retained profile facts and command receipts are immutable' USING ERRCODE='23514'; END;
$$;
CREATE TRIGGER immutable_policy_revision BEFORE UPDATE OR DELETE ON profiles.policy_revisions FOR EACH ROW EXECUTE FUNCTION profiles.reject_retained_mutation();
CREATE TRIGGER immutable_publication BEFORE UPDATE OR DELETE ON profiles.publications FOR EACH ROW EXECUTE FUNCTION profiles.reject_retained_mutation();
CREATE TRIGGER immutable_profile_receipt BEFORE UPDATE OR DELETE ON profiles.command_receipts FOR EACH ROW EXECUTE FUNCTION profiles.reject_retained_mutation();
CREATE FUNCTION profiles.guard_policy_head() RETURNS trigger LANGUAGE plpgsql AS $$
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
            AND p.revision=NEW.revision AND p.require_reference=NEW.require_reference) THEN
        RAISE EXCEPTION 'Policy publication must retain the exact current typed setting' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER protect_policy_head BEFORE INSERT OR UPDATE OR DELETE ON profiles.policy_heads FOR EACH ROW EXECUTE FUNCTION profiles.guard_policy_head();
CREATE FUNCTION profiles.guard_authority() RETURNS trigger LANGUAGE plpgsql AS $$
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
        WHERE p.tenant_id=NEW.tenant_id AND p.profile_id=NEW.legacy_baseline_profile_id AND p.legacy_baseline AND NOT s.require_reference) THEN
        RAISE EXCEPTION 'Legacy baseline must be an explicit optional retained profile' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER protect_authority BEFORE INSERT OR UPDATE OR DELETE ON profiles.authority FOR EACH ROW EXECUTE FUNCTION profiles.guard_authority();
DO $$ DECLARE target text; BEGIN
    FOREACH target IN ARRAY ARRAY['policy_heads','policy_revisions','publications','authority','command_receipts'] LOOP
        EXECUTE format('ALTER TABLE profiles.%I ENABLE ROW LEVEL SECURITY',target);
        EXECUTE format('ALTER TABLE profiles.%I FORCE ROW LEVEL SECURITY',target);
        EXECUTE format('CREATE POLICY tenant_isolation ON profiles.%I USING (tenant_id = NULLIF(current_setting(''app.current_tenant'',true),'''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.current_tenant'',true),'''')::uuid)',target);
    END LOOP;
END $$;
REVOKE ALL ON ALL TABLES IN SCHEMA profiles FROM PUBLIC;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA profiles FROM PUBLIC;
