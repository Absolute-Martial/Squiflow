SELECT EXISTS(SELECT 1 FROM customers.import_rows r
  WHERE r.tenant_id=@tenant_id AND r.import_id=@import_id AND r.status=1 AND NOT r.requires_decision
    AND EXISTS(SELECT 1 FROM customers.individuals i WHERE i.tenant_id=r.tenant_id
      AND (i.normalized_name=r.name_signal OR i.normalized_email=r.email_signal
        OR i.normalized_phone=r.phone_signal OR i.normalized_external_registration_id=r.external_id_signal)));
