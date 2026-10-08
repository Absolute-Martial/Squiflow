SELECT source_unit_id, target_unit_id, revision, numerator, denominator, published_by_account_id, published_at
FROM catalog.unit_conversions
WHERE tenant_id = @tenant_id AND source_unit_id = @source_unit_id
  AND target_unit_id = @target_unit_id AND revision = @revision;
