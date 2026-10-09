SELECT evidence.response_json::text,evidence.request_id,evidence.request_revision,evidence.order_revision,
    evidence.subject_fingerprint,event.response_json::text AS approval_response
FROM orders.approval_commit_evidence evidence JOIN orders.approval_events event
    ON event.tenant_id=evidence.tenant_id AND event.request_id=evidence.request_id AND event.request_revision=evidence.request_revision
WHERE evidence.tenant_id=@tenant_id AND evidence.order_id=@order_id;
