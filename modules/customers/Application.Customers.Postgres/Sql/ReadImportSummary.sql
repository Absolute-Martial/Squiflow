SELECT i.contract_version,i.manifest_hash,c.total,c.pending,c.imported,c.mapped,c.rejected,c.failed,
       w.work_id,w.status,w.created_at,w.completed_at,w.last_error
FROM customers.imports i
CROSS JOIN LATERAL (
 SELECT count(*)::int AS total,count(*) FILTER(WHERE status=1)::int AS pending,
        count(*) FILTER(WHERE status=2)::int AS imported,count(*) FILTER(WHERE status=3)::int AS mapped,
        count(*) FILTER(WHERE status=4)::int AS rejected,count(*) FILTER(WHERE status=5)::int AS failed
 FROM customers.import_rows r WHERE r.tenant_id=i.tenant_id AND r.import_id=i.import_id
) c
LEFT JOIN customers.import_work w ON w.tenant_id=i.tenant_id AND w.import_id=i.import_id
WHERE i.tenant_id=@tenant_id AND i.import_id=@import_id;
