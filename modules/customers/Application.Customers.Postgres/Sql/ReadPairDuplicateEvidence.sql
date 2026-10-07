SELECT COALESCE(a.normalized_email=b.normalized_email,false),COALESCE(a.normalized_phone=b.normalized_phone,false),
       COALESCE(a.normalized_external_registration_id=b.normalized_external_registration_id,false),
       a.normalized_name=b.normalized_name,
       EXISTS(SELECT 1 FROM customers.representatives x JOIN customers.representatives y
         ON y.tenant_id=x.tenant_id AND y.organization_id=x.organization_id
         WHERE x.tenant_id=@tenant_id AND x.individual_id=@first_id AND y.individual_id=@second_id
           AND x.availability=1 AND y.availability=1),
       EXISTS(SELECT 1 FROM customers.representatives x JOIN customers.representatives y
         ON y.tenant_id=x.tenant_id AND y.program_id=x.program_id
         WHERE x.tenant_id=@tenant_id AND x.individual_id=@first_id AND y.individual_id=@second_id
           AND x.availability=1 AND y.availability=1)
FROM customers.individuals a JOIN customers.individuals b ON b.tenant_id=a.tenant_id AND b.id=@second_id
WHERE a.tenant_id=@tenant_id AND a.id=@first_id;
