SELECT c.facts::text,i.facts::text,r.facts::text,c.facts_version,i.facts_version,r.facts_version,r.kind,i.revision
FROM quotations.conversions c JOIN quotations.issued i ON i.tenant_id=c.tenant_id AND i.id=c.issued_revision_id
JOIN quotations.responses r ON r.tenant_id=c.tenant_id AND r.issued_revision_id=c.issued_revision_id
WHERE c.tenant_id=@tenant AND c.order_id=@order_id AND c.quotation_id=@id AND c.issued_revision_id=@revision_id;
