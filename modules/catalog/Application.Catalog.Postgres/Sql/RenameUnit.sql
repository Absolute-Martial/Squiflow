UPDATE catalog.units
SET name = @name, revision = revision + 1
WHERE tenant_id = @tenant_id AND id = @unit_id
  AND status = 'active' AND revision = @expected_revision
RETURNING id;
