INSERT INTO invoices.customer_payment_events(tenant_id,event_id,payment_id,revision,operation,state,actor_account_id,recorded_at,authorization_revision,evidence,receivable_id,amount,allocation_id,schema_version)
VALUES(@tenant,@event,@payment,@revision,@operation,@state,@actor,@at,@authorization,@evidence,@receivable,@amount,@allocation,1);
