UPDATE customers.individuals SET redirect_target_individual_id = @canonical_id, revision = revision + 1
WHERE tenant_id = @tenant_id AND id = @source_id AND redirect_target_individual_id IS NULL;
-- History is append-only. Advance only current pointers, after the source's new
-- ledger entry exists; deferred guards check the final one-hop state at commit.
UPDATE customers.individuals SET redirect_target_individual_id = @canonical_id, revision = revision + 1
WHERE tenant_id = @tenant_id AND redirect_target_individual_id = @source_id;
