SELECT r.facts::text,i.facts::text,r.facts_version,r.kind,i.facts_version,i.revision
FROM quotations.responses r JOIN quotations.issued i ON i.tenant_id=r.tenant_id AND i.id=r.issued_revision_id
WHERE r.tenant_id=@tenant AND r.quotation_id=@id AND r.issued_revision_id=@revision_id;
