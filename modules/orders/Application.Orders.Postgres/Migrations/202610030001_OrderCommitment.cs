using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class OrderCommitment : Migration
{
    internal const string LifecycleConstraint =
        "(state = 'draft' AND revision >= 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL AND committed_at IS NULL AND committed_by_account_id IS NULL) OR " +
        "(state = 'abandoned' AND revision >= 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL AND committed_at IS NULL AND committed_by_account_id IS NULL) OR " +
        "(state = 'committed' AND revision >= 2 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL AND committed_at IS NOT NULL AND committed_by_account_id IS NOT NULL)";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("committed_at", "order_drafts", "timestamp with time zone", schema: "orders", nullable: true);
        migrationBuilder.AddColumn<Guid>("committed_by_account_id", "order_drafts", "uuid", schema: "orders", nullable: true);
        migrationBuilder.CreateIndex("IX_order_drafts_committed_by_account_id", "order_drafts", "committed_by_account_id", schema: "orders");
        migrationBuilder.AddForeignKey("fk_order_drafts_accounts_committed_by_account_id", "order_drafts", "committed_by_account_id", "accounts", schema: "orders", principalSchema: "identity_access", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.DropCheckConstraint("ck_order_drafts_lifecycle", "order_drafts", "orders");
        migrationBuilder.AddCheckConstraint("ck_order_drafts_lifecycle", "order_drafts", LifecycleConstraint, "orders");
        migrationBuilder.Sql("""
            CREATE FUNCTION orders.protect_committed_order() RETURNS trigger
            LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    IF NEW.state <> 'draft' THEN
                        RAISE EXCEPTION 'An order must begin as a draft.' USING ERRCODE = '23514';
                    END IF;
                ELSE
                    IF OLD.state IN ('committed', 'abandoned') THEN
                        RAISE EXCEPTION 'Terminal order facts are immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF NEW.state = 'committed' AND (
                        NEW.revision <> OLD.revision + 1 OR
                        ROW(NEW.tenant_id, NEW.id, NEW.created_by_account_id, NEW.created_at,
                            NEW.summary, NEW.currency_code, NEW.total,
                            NEW.customer_organization_id, NEW.customer_program_id)
                        IS DISTINCT FROM
                        ROW(OLD.tenant_id, OLD.id, OLD.created_by_account_id, OLD.created_at,
                            OLD.summary, OLD.currency_code, OLD.total,
                            OLD.customer_organization_id, OLD.customer_program_id)) THEN
                        RAISE EXCEPTION 'Commitment must preserve the current order facts.' USING ERRCODE = '23514';
                    END IF;
                END IF;
                RETURN NEW;
            END
            $body$;
            CREATE TRIGGER protect_committed_order
                BEFORE INSERT OR UPDATE ON orders.order_drafts
                FOR EACH ROW EXECUTE FUNCTION orders.protect_committed_order();
            CREATE POLICY order_draft_lines_draft_update ON orders.order_draft_lines
            AS RESTRICTIVE FOR UPDATE
            USING (EXISTS (
                SELECT 1 FROM orders.order_drafts AS draft
                WHERE draft.tenant_id = order_draft_lines.tenant_id
                  AND draft.id = order_draft_lines.order_id AND draft.state = 'draft'
                FOR UPDATE
            ))
            WITH CHECK (EXISTS (
                SELECT 1 FROM orders.order_drafts AS draft
                WHERE draft.tenant_id = order_draft_lines.tenant_id
                  AND draft.id = order_draft_lines.order_id AND draft.state = 'draft'
                FOR UPDATE
            ));
            CREATE POLICY order_draft_lines_draft_insert ON orders.order_draft_lines
            AS RESTRICTIVE FOR INSERT
            WITH CHECK (EXISTS (
                SELECT 1 FROM orders.order_drafts AS draft
                WHERE draft.tenant_id = order_draft_lines.tenant_id
                  AND draft.id = order_draft_lines.order_id AND draft.state = 'draft'
                FOR UPDATE
            ));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retained commitments cannot be represented by the previous schema. Rollback must
        // fail transactionally rather than silently erase their authority or historical receipts.
        migrationBuilder.Sql("""
            LOCK TABLE orders.order_drafts, orders.command_receipts IN ACCESS EXCLUSIVE MODE;
            DO $body$
            DECLARE current_tenant uuid;
            BEGIN
                -- The schema identity need not bypass forced tenant RLS. Check every owned
                -- tenant partition, under table locks, before removing commitment authority.
                FOR current_tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant', current_tenant::text, true);
                    IF EXISTS (SELECT 1 FROM orders.order_drafts
                               WHERE tenant_id = current_tenant AND state = 'committed')
                       OR EXISTS (SELECT 1 FROM orders.command_receipts
                                  WHERE tenant_id = current_tenant AND operation = 'commit-order-draft') THEN
                        RAISE EXCEPTION 'Cannot remove the commitment schema while committed orders exist.';
                    END IF;
                END LOOP;
            END $body$;
            DROP POLICY order_draft_lines_draft_update ON orders.order_draft_lines;
            DROP POLICY order_draft_lines_draft_insert ON orders.order_draft_lines;
            DROP TRIGGER protect_committed_order ON orders.order_drafts;
            DROP FUNCTION orders.protect_committed_order();
            """);
        migrationBuilder.DropCheckConstraint("ck_order_drafts_lifecycle", "order_drafts", "orders");
        migrationBuilder.AddCheckConstraint("ck_order_drafts_lifecycle", "order_drafts",
            "(state = 'draft' AND revision >= 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL) OR " +
            "(state = 'abandoned' AND revision >= 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL)", "orders");
        migrationBuilder.DropForeignKey("fk_order_drafts_accounts_committed_by_account_id", "order_drafts", "orders");
        migrationBuilder.DropIndex("IX_order_drafts_committed_by_account_id", "order_drafts", "orders");
        migrationBuilder.DropColumn("committed_at", "order_drafts", "orders");
        migrationBuilder.DropColumn("committed_by_account_id", "order_drafts", "orders");
    }
}
