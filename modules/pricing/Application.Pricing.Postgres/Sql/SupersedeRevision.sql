UPDATE pricing.price_revisions
SET state = 'superseded'
WHERE tenant_id = @tenant_id AND revision_id = @revision_id AND state = 'published';
