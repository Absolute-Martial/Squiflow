UPDATE pricing.price_revisions
SET state = 'published', published_at = @published_at
WHERE tenant_id = @tenant_id AND revision_id = @revision_id AND state = 'draft';
