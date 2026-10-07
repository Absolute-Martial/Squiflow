INSERT INTO catalog.unit_conversions
    (tenant_id, source_unit_id, target_unit_id, revision, numerator, denominator, published_by_account_id, published_at)
VALUES (@tenant_id, @source_unit_id, @target_unit_id, @revision, @numerator, @denominator, @account_id, @published_at);
