SELECT account_id FROM identity_access.external_identity_bindings
WHERE issuer = @issuer AND subject = @subject;
