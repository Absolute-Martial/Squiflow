UPDATE pricing.price_revisions
SET state = 'retired', retired_at = @retired_at
WHERE tenant_id = @tenant_id AND revision_id = @revision_id AND state = 'published';
