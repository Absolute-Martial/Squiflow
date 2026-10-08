WITH duplicate_names AS (SELECT name_signal FROM customers.import_rows WHERE tenant_id=@tenant_id AND import_id=@import_id AND status=1 GROUP BY name_signal HAVING count(*)>1),
duplicate_emails AS (SELECT email_signal FROM customers.import_rows WHERE tenant_id=@tenant_id AND import_id=@import_id AND status=1 AND email_signal IS NOT NULL GROUP BY email_signal HAVING count(*)>1),
duplicate_phones AS (SELECT phone_signal FROM customers.import_rows WHERE tenant_id=@tenant_id AND import_id=@import_id AND status=1 AND phone_signal IS NOT NULL GROUP BY phone_signal HAVING count(*)>1),
duplicate_external AS (SELECT external_id_signal FROM customers.import_rows WHERE tenant_id=@tenant_id AND import_id=@import_id AND status=1 AND external_id_signal IS NOT NULL GROUP BY external_id_signal HAVING count(*)>1),
flags AS (
 SELECT r.row_number,
   (r.name_signal IN (SELECT name_signal FROM duplicate_names) OR EXISTS(SELECT 1 FROM customers.individuals i WHERE i.tenant_id=@tenant_id AND i.normalized_name=r.name_signal)) AS name_match,
   (r.email_signal IN (SELECT email_signal FROM duplicate_emails) OR EXISTS(SELECT 1 FROM customers.individuals i WHERE i.tenant_id=@tenant_id AND i.normalized_email=r.email_signal)) AS email_match,
   (r.phone_signal IN (SELECT phone_signal FROM duplicate_phones) OR EXISTS(SELECT 1 FROM customers.individuals i WHERE i.tenant_id=@tenant_id AND i.normalized_phone=r.phone_signal)) AS phone_match,
   (r.external_id_signal IN (SELECT external_id_signal FROM duplicate_external) OR EXISTS(SELECT 1 FROM customers.individuals i WHERE i.tenant_id=@tenant_id AND i.normalized_external_registration_id=r.external_id_signal)) AS external_match
 FROM customers.import_rows r WHERE tenant_id=@tenant_id AND import_id=@import_id AND status=1
)
UPDATE customers.import_rows r SET requires_decision=true,
 duplicate_evidence=concat_ws(',',CASE WHEN f.name_match THEN 'ExactName' END,CASE WHEN f.email_match THEN 'NormalizedEmail' END,
   CASE WHEN f.phone_match THEN 'NormalizedPhone' END,CASE WHEN f.external_match THEN 'ExternalRegistrationId' END)
FROM flags f WHERE r.tenant_id=@tenant_id AND r.import_id=@import_id AND r.row_number=f.row_number
 AND (f.name_match OR f.email_match OR f.phone_match OR f.external_match);
