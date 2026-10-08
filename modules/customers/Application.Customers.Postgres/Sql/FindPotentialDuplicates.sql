SELECT i.id, i.display_name, i.email, i.phone, i.availability, i.revision,
       i.created_by_account_id, i.created_at, i.availability_changed_by_account_id,
       i.availability_changed_at, i.contact_changed_by_account_id, i.contact_changed_at,
       i.customer_type, i.external_registration_id, i.address_line1, i.address_line2,
       i.city, i.notes, i.redirect_target_individual_id, i.normalized_name AS candidate_normalized_name,
       COALESCE(@email IS NOT NULL AND i.normalized_email = @email, false) AS email_match,
       COALESCE(@phone IS NOT NULL AND i.normalized_phone = @phone, false) AS phone_match,
       COALESCE(@external_id IS NOT NULL AND i.normalized_external_registration_id = @external_id, false) AS external_match,
       (@name IS NOT NULL AND i.normalized_name = @name) AS name_match,
       EXISTS (SELECT 1 FROM customers.representatives r
               WHERE r.tenant_id = i.tenant_id AND r.individual_id = i.id
                 AND r.availability = 1 AND (@organization_id IS NOT NULL AND r.organization_id = @organization_id)) AS organization_match,
       EXISTS (SELECT 1 FROM customers.representatives r
               WHERE r.tenant_id = i.tenant_id AND r.individual_id = i.id
                 AND r.availability = 1 AND (@program_id IS NOT NULL AND r.program_id = @program_id)) AS program_match,
       retained.resolution_id, retained.customer_id AS resolved_customer_id,
       retained.other_customer_id AS resolved_other_customer_id, retained.outcome AS resolved_outcome,
       retained.account_id AS resolved_account_id, retained.resolved_at, retained.reason AS resolved_reason,
       retained.evidence AS resolved_evidence
FROM customers.individuals i
LEFT JOIN LATERAL (SELECT receipt.* FROM customers.duplicate_command_receipts receipt
    WHERE receipt.tenant_id=i.tenant_id AND @exclude_id IS NOT NULL
      AND ((receipt.customer_id=i.id AND receipt.other_customer_id=@exclude_id)
         OR (receipt.other_customer_id=i.id AND receipt.customer_id=@exclude_id))
    ORDER BY receipt.resolved_at DESC,receipt.resolution_id DESC LIMIT 1) retained ON true
WHERE i.tenant_id = @tenant_id
  AND (@exclude_id IS NULL OR i.id <> @exclude_id)
  -- A consolidated source is not a candidate: the identity it matched has moved to its
  -- successor, so offering it lets an operator select a target whose mutations are refused.
  -- Its successor is itself a live candidate on the same signals.
  AND i.redirect_target_individual_id IS NULL
  AND (
      (@email IS NOT NULL AND i.normalized_email = @email)
      OR (@phone IS NOT NULL AND i.normalized_phone = @phone)
      OR (@external_id IS NOT NULL AND i.normalized_external_registration_id = @external_id)
       OR (@name IS NOT NULL AND i.normalized_name = @name)
       OR (@include_fuzzy AND @name IS NOT NULL AND left(i.normalized_name, 1) = left(@name, 1))
  )
ORDER BY i.created_at DESC, i.id DESC
LIMIT @limit;
