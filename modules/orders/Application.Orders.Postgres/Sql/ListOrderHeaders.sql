SELECT d.id,d.summary,d.currency_code,d.total,d.revision,d.created_at,d.state,d.abandoned_at,
       d.customer_organization_id,d.customer_program_id,d.committed_at,d.committed_by_account_id,
       o.quotation_id,o.issued_revision_id,o.number AS quotation_number,o.revision_number AS quotation_revision_number
FROM orders.order_drafts d LEFT JOIN orders.quotation_origins o ON o.tenant_id=d.tenant_id AND o.order_id=d.id
WHERE d.tenant_id=@tenant_id AND (@after_created_at IS NULL OR d.created_at<@after_created_at
       OR (d.created_at=@after_created_at AND d.id<@after_id))
ORDER BY d.created_at DESC,d.id DESC LIMIT @limit;
