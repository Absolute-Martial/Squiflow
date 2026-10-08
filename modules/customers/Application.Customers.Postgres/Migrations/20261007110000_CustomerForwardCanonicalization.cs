using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Customers.Postgres.Migrations;

[Migration("20261007110000_CustomerForwardCanonicalization")]
// Only trigger/function semantics change. Preserve the previous immutable EF target
// model (and inherited DbContext attribute) and snapshot; customer_redirects remains
// the original append-only ledger.
public sealed class CustomerForwardCanonicalization : CustomerImportFencedExecution
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION customers.serialize_canonical_mutation() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $$
            DECLARE tenant uuid := NULLIF(current_setting('app.current_tenant',true),'')::uuid;
            BEGIN
              IF tenant IS NOT NULL THEN
                PERFORM pg_advisory_xact_lock(hashtextextended('customers:canonical:' || tenant::text,0));
              END IF;
              RETURN NULL;
            END $$;
            -- Statement gates run before tuple locks, including contact/availability
            -- edits and unlink, avoiding opposite row/gate acquisition orders.
            CREATE TRIGGER serialize_individual_canonical_mutation BEFORE UPDATE ON customers.individuals
              FOR EACH STATEMENT EXECUTE FUNCTION customers.serialize_canonical_mutation();
            CREATE TRIGGER serialize_representative_canonical_mutation BEFORE INSERT OR UPDATE ON customers.representatives
              FOR EACH STATEMENT EXECUTE FUNCTION customers.serialize_canonical_mutation();
            CREATE TRIGGER serialize_redirect_canonical_mutation BEFORE INSERT ON customers.customer_redirects
              FOR EACH STATEMENT EXECUTE FUNCTION customers.serialize_canonical_mutation();

            CREATE OR REPLACE FUNCTION customers.protect_redirect_shape() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $$
            BEGIN
              PERFORM id FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                AND id IN (NEW.source_customer_id,NEW.canonical_customer_id) ORDER BY id FOR UPDATE;
              IF EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                AND id IN (NEW.source_customer_id,NEW.canonical_customer_id) AND redirect_target_individual_id IS NOT NULL) THEN
                RAISE EXCEPTION 'New customer redirect requires explicit current identities' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END $$;

            CREATE OR REPLACE FUNCTION customers.protect_individual_redirect() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $$
            BEGIN
              IF OLD.redirect_target_individual_id IS NOT NULL THEN
                IF (to_jsonb(NEW)-'redirect_target_individual_id'-'revision') IS DISTINCT FROM
                   (to_jsonb(OLD)-'redirect_target_individual_id'-'revision') THEN
                  RAISE EXCEPTION 'Redirected customer master facts are retained' USING ERRCODE='23514';
                END IF;
                IF NEW.redirect_target_individual_id IS DISTINCT FROM OLD.redirect_target_individual_id THEN
                  IF NEW.redirect_target_individual_id IS NULL OR NEW.revision<>OLD.revision+1
                    OR NOT EXISTS(SELECT 1 FROM customers.customer_redirects r
                      WHERE r.tenant_id=OLD.tenant_id AND r.source_customer_id=OLD.redirect_target_individual_id
                        AND r.canonical_customer_id=NEW.redirect_target_individual_id)
                       AND NOT EXISTS(SELECT 1 FROM customers.individuals previous
                         WHERE previous.tenant_id=OLD.tenant_id AND previous.id=OLD.redirect_target_individual_id
                           AND previous.redirect_target_individual_id=NEW.redirect_target_individual_id) THEN
                    RAISE EXCEPTION 'Current customer successor can only advance with retained consolidation' USING ERRCODE='23514';
                  END IF;
                ELSIF NEW.revision<>OLD.revision THEN
                  RAISE EXCEPTION 'Retained customer revision requires successor advancement' USING ERRCODE='23514';
                END IF;
              ELSIF NEW.redirect_target_individual_id IS NOT NULL THEN
                IF (to_jsonb(NEW)-'redirect_target_individual_id'-'revision') IS DISTINCT FROM
                   (to_jsonb(OLD)-'redirect_target_individual_id'-'revision')
                  OR NEW.revision<>OLD.revision+1 OR NOT EXISTS(SELECT 1 FROM customers.customer_redirects
                  WHERE tenant_id=NEW.tenant_id AND source_customer_id=NEW.id
                    AND canonical_customer_id=NEW.redirect_target_individual_id) THEN
                  RAISE EXCEPTION 'Customer redirect requires its atomic ledger' USING ERRCODE='23514';
                END IF;
              END IF;
              RETURN NEW;
            END $$;

            CREATE FUNCTION customers.validate_current_customer_redirect() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $$
            DECLARE current_pointer uuid;
            BEGIN
              SELECT redirect_target_individual_id INTO current_pointer FROM customers.individuals
                WHERE tenant_id=NEW.tenant_id AND id=NEW.id;
              IF current_pointer IS NOT NULL AND (
                NOT EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                  AND id=current_pointer AND redirect_target_individual_id IS NULL)
                OR NOT EXISTS(SELECT 1 FROM customers.customer_redirects WHERE tenant_id=NEW.tenant_id AND source_customer_id=NEW.id)
                OR EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=NEW.tenant_id AND redirect_target_individual_id=NEW.id)
                OR EXISTS(SELECT 1 FROM customers.representatives WHERE tenant_id=NEW.tenant_id AND individual_id=NEW.id AND availability=1)) THEN
                RAISE EXCEPTION 'Current customer successors require flattened pointers and canonical live references' USING ERRCODE='23514';
              END IF;
              RETURN NULL;
            END $$;
            CREATE CONSTRAINT TRIGGER validate_current_customer_redirect AFTER INSERT OR UPDATE ON customers.individuals
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION customers.validate_current_customer_redirect();

            CREATE FUNCTION customers.validate_customer_redirect_ledger() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $$
            BEGIN
              IF NOT EXISTS(SELECT 1 FROM customers.individuals source
                JOIN customers.individuals target ON target.tenant_id=source.tenant_id AND target.id=NEW.canonical_customer_id
                WHERE source.tenant_id=NEW.tenant_id AND source.id=NEW.source_customer_id
                  AND source.redirect_target_individual_id=COALESCE(target.redirect_target_individual_id,target.id)) THEN
                RAISE EXCEPTION 'Retained redirect requires an atomic current successor' USING ERRCODE='23514';
              END IF;
              RETURN NULL;
            END $$;
            CREATE CONSTRAINT TRIGGER validate_customer_redirect_ledger AFTER INSERT ON customers.customer_redirects
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION customers.validate_customer_redirect_ledger();

            CREATE FUNCTION customers.protect_consolidation_history() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $$
            BEGIN
              RAISE EXCEPTION 'Retained customer consolidation evidence is immutable' USING ERRCODE='23514';
            END $$;
            CREATE TRIGGER protect_customer_redirect_history BEFORE UPDATE OR DELETE ON customers.customer_redirects
              FOR EACH ROW EXECUTE FUNCTION customers.protect_consolidation_history();
            CREATE TRIGGER protect_duplicate_receipt_history BEFORE UPDATE OR DELETE ON customers.duplicate_command_receipts
              FOR EACH ROW EXECUTE FUNCTION customers.protect_consolidation_history();
            REVOKE ALL ON FUNCTION customers.serialize_canonical_mutation(),customers.validate_current_customer_redirect(),
              customers.validate_customer_redirect_ledger(),customers.protect_consolidation_history() FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE customers.individuals,customers.customer_redirects,customers.representatives IN ACCESS EXCLUSIVE MODE;
            DO $guard$ DECLARE tenant uuid; BEGIN
              FOR tenant IN SELECT id FROM tenancy.tenants LOOP
                PERFORM set_config('app.current_tenant',tenant::text,true);
                IF EXISTS(SELECT 1 FROM customers.customer_redirects r JOIN customers.individuals i
                  ON i.tenant_id=r.tenant_id AND i.id=r.source_customer_id WHERE r.tenant_id=tenant
                    AND i.redirect_target_individual_id IS DISTINCT FROM r.canonical_customer_id) THEN
                  RAISE EXCEPTION 'Cannot downgrade forward-canonicalized Customers history';
                END IF;
              END LOOP;
            END $guard$;
            DROP TRIGGER serialize_individual_canonical_mutation ON customers.individuals;
            DROP TRIGGER serialize_representative_canonical_mutation ON customers.representatives;
            DROP TRIGGER serialize_redirect_canonical_mutation ON customers.customer_redirects;
            DROP TRIGGER validate_current_customer_redirect ON customers.individuals;
            DROP TRIGGER validate_customer_redirect_ledger ON customers.customer_redirects;
            DROP TRIGGER protect_customer_redirect_history ON customers.customer_redirects;
            DROP TRIGGER protect_duplicate_receipt_history ON customers.duplicate_command_receipts;
            DROP FUNCTION customers.serialize_canonical_mutation(),customers.validate_current_customer_redirect(),
              customers.validate_customer_redirect_ledger(),customers.protect_consolidation_history();
            CREATE OR REPLACE FUNCTION customers.protect_redirect_shape() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              PERFORM id FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                AND id IN(NEW.source_customer_id,NEW.canonical_customer_id) ORDER BY id FOR UPDATE;
              IF EXISTS(SELECT 1 FROM customers.customer_redirects WHERE tenant_id=NEW.tenant_id AND canonical_customer_id=NEW.source_customer_id)
                OR EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                  AND id IN(NEW.source_customer_id,NEW.canonical_customer_id) AND redirect_target_individual_id IS NOT NULL) THEN
                RAISE EXCEPTION 'Customer redirect chains are not supported' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END $$;
            CREATE OR REPLACE FUNCTION customers.protect_individual_redirect() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF OLD.redirect_target_individual_id IS NOT NULL AND NEW IS DISTINCT FROM OLD THEN
                RAISE EXCEPTION 'Redirected customer master facts are retained' USING ERRCODE='23514';
              END IF;
              IF NEW.redirect_target_individual_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM customers.customer_redirects
                WHERE tenant_id=NEW.tenant_id AND source_customer_id=NEW.id AND canonical_customer_id=NEW.redirect_target_individual_id) THEN
                RAISE EXCEPTION 'Customer redirect requires its atomic ledger' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END $$;
            """);
    }
}
