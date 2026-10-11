SELECT h.id,h.created_by,h.created_at,h.version,h.number,h.draft::text,i.facts::text,h.last_issued_revision,i.id,h.facts_version,i.facts_version,
       r.facts::text,r.facts_version,r.issued_revision_id,c.facts::text,c.facts_version,c.issued_revision_id,c.order_id,r.kind
FROM quotations.heads h LEFT JOIN quotations.issued i ON i.tenant_id=h.tenant_id AND i.id=h.current_issued_id
LEFT JOIN quotations.responses r ON r.tenant_id=h.tenant_id AND r.issued_revision_id=h.current_issued_id
LEFT JOIN quotations.conversions c ON c.tenant_id=h.tenant_id AND c.issued_revision_id=h.current_issued_id
WHERE h.tenant_id=@tenant AND h.id=@id;
