UPDATE customers.import_work SET status=1,last_error=NULL,authorization_revision=@authority_revision
WHERE tenant_id=@tenant_id AND import_id=@import_id AND account_id=@account_id AND last_error='authority_changed';
