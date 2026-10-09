SELECT draft.tenant_id, draft.id, draft.revision, line.position
FROM orders.order_drafts AS draft
JOIN orders.order_draft_lines AS line
  ON line.tenant_id = draft.tenant_id
 AND line.order_id = draft.id
WHERE draft.tenant_id = @tenant_id
  AND draft.id = @order_id
  AND draft.revision = @expected_revision
  AND draft.state = 'committed'
  AND line.position = @line_position;
