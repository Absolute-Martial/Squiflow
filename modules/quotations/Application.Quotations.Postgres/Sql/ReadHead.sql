SELECT h.id,h.created_by,h.created_at,h.version,h.number,h.draft::text,i.facts::text,h.last_issued_revision,i.id,h.facts_version,i.facts_version
FROM quotations.heads h LEFT JOIN quotations.issued i ON i.tenant_id=h.tenant_id AND i.id=h.current_issued_id
WHERE h.tenant_id=@tenant AND h.id=@id;
