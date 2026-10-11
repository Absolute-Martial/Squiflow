SELECT fingerprint,response::text,version FROM quotations.receipts WHERE tenant_id=@tenant AND account_id=@actor AND operation=@operation AND key=@key;
