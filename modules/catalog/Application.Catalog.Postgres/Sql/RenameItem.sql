UPDATE catalog.items
SET name = @name, description = @description, revision = revision + 1
WHERE tenant_id = @tenant_id AND id = @item_id
  AND status = 'active' AND revision = @expected_revision
RETURNING id;
