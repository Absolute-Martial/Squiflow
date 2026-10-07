SELECT facts::text,id,revision,facts_version FROM quotations.issued WHERE tenant_id=@tenant AND quotation_id=@id AND revision>@after ORDER BY revision LIMIT @limit;
