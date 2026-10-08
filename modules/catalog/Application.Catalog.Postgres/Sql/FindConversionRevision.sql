SELECT COALESCE(MAX(revision), 0)
FROM catalog.unit_conversions
WHERE tenant_id = @tenant_id AND source_unit_id = @source_unit_id AND target_unit_id = @target_unit_id;
